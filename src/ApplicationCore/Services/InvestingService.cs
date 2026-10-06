using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Default implementation of <see cref="IInvestingService"/>. All interaction with Upvest goes
/// through <see cref="IUpvestClient"/>. The background reconciler drives async state forward, so
/// placing an order never has to wait on — or fail because of — anything to do with investing.
/// </summary>
public class InvestingService : IInvestingService
{
    // Serialises the mutating sections across the background reconciler and webhook deliveries,
    // which may run concurrently against the same shopper.
    private static readonly SemaphoreSlim _gate = new(1, 1);

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestClient _upvest;
    private readonly InvestingSettings _settings;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IRepository<Investment> investments,
        IUpvestClient upvest,
        InvestingSettings settings,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _investments = investments;
        _upvest = upvest;
        _settings = settings;
        _logger = logger;
    }

    public async Task<Investor> EnrolAsync(string buyerId, InvestorSignup signup, CancellationToken cancellationToken)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (existing is not null)
        {
            // Already opted in — enrolment is idempotent; return the current state rather than re-onboarding.
            return existing;
        }

        var investor = await _investors.AddAsync(new Investor(buyerId), cancellationToken);
        try
        {
            var user = await _upvest.CreateUserAsync(signup, cancellationToken);
            investor.LinkUpvestUser(user.Id);
            ApplyUserStatus(investor, user.Status);

            await _upvest.SubmitKycCheckAsync(user.Id, signup, cancellationToken);
            await _upvest.SetTaxResidencyAsync(user.Id, signup, cancellationToken);

            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogInformation("Investor {InvestorId} enrolled; Upvest user created, onboarding submitted.", investor.Id);
            return investor;
        }
        catch (Exception ex)
        {
            // Roll back the dangling local enrolment so the shopper can retry cleanly.
            _logger.LogWarning("Enrolment with Upvest failed for investor {InvestorId}: {Error}", investor.Id, ex.Message);
            await _investors.DeleteAsync(investor, cancellationToken);
            throw;
        }
    }

    public Task<Investor?> GetInvestorAsync(string buyerId, CancellationToken cancellationToken)
        => _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);

    public async Task<decimal> RecordPaidOrderAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken)
    {
        // Serialise with the reconciler so a concurrent investor save cannot clobber this increment.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);

            // Only an accepted investor sets anything aside.
            if (investor is null || !investor.IsAcceptedInvestor)
            {
                return 0m;
            }

            var roundUp = RoundUp(orderTotal);
            if (roundUp <= 0m)
            {
                return 0m;
            }

            investor.SetAsideChange(roundUp);
            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogInformation("Set aside {RoundUp} for investor {InvestorId}; pending balance now {Pending}.",
                roundUp, investor.Id, investor.PendingAmount);
            return roundUp;
        }
        catch (Exception ex)
        {
            // Setting aside change must never disrupt the order. Swallow and report nothing set aside.
            _logger.LogWarning("Could not set aside change for buyer: {Error}", ex.Message);
            return 0m;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken)
    {
        var list = await _investments.ListAsync(new InvestmentsByBuyerIdSpecification(buyerId), cancellationToken);
        return list;
    }

    public async Task<BalanceSummary> GetBalanceAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        var pending = investor?.PendingAmount ?? 0m;

        var investments = await _investments.ListAsync(new InvestmentsByBuyerIdSpecification(buyerId), cancellationToken);
        var invested = investments
            .Where(i => i.Status == InvestmentStatus.Settled)
            .Sum(i => i.Amount);

        return new BalanceSummary(decimal.Round(pending, 2), decimal.Round(invested, 2));
    }

    public async Task ProcessDueWorkAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await ActivatePendingEnrolmentsAsync(cancellationToken);
            await ProvisionAccountsAsync(cancellationToken);
            await InvestDueBalancesAsync(cancellationToken);
            await SettlePlacedInvestmentsAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task HandleWebhookEventAsync(string eventType, string? resourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(eventType)) return;
        var domain = eventType.Split('.')[0].ToUpperInvariant();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            switch (domain)
            {
                case "USER" when !string.IsNullOrEmpty(resourceId):
                {
                    var investor = await _investors.FirstOrDefaultAsync(new InvestorByUpvestUserIdSpecification(resourceId!), cancellationToken);
                    if (investor is not null && investor.Status == EnrolmentStatus.Pending)
                    {
                        await ReconcileInvestorActivationAsync(investor, cancellationToken);
                    }
                    break;
                }
                case "ORDER" when !string.IsNullOrEmpty(resourceId):
                {
                    var investment = await _investments.FirstOrDefaultAsync(new InvestmentByUpvestOrderIdSpecification(resourceId!), cancellationToken);
                    if (investment is not null && investment.Status == InvestmentStatus.Pending)
                    {
                        await ReconcileInvestmentAsync(investment, cancellationToken);
                    }
                    break;
                }
                // ACCOUNT / ACCOUNT_GROUP / EXECUTION events need no direct action here —
                // the next reconciler tick advances provisioning and settlement.
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // --- internal steps -----------------------------------------------------------------

    private async Task ActivatePendingEnrolmentsAsync(CancellationToken ct)
    {
        var pending = await _investors.ListAsync(new InvestorsByStatusSpecification(EnrolmentStatus.Pending), ct);
        foreach (var investor in pending)
        {
            if (string.IsNullOrEmpty(investor.UpvestUserId)) continue;
            try
            {
                await ReconcileInvestorActivationAsync(investor, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not reconcile enrolment {InvestorId}: {Error}", investor.Id, ex.Message);
            }
        }
    }

    private async Task ReconcileInvestorActivationAsync(Investor investor, CancellationToken ct)
    {
        var user = await _upvest.GetUserAsync(investor.UpvestUserId!, ct);
        var before = investor.Status;
        ApplyUserStatus(investor, user.Status);
        if (investor.Status != before)
        {
            await _investors.UpdateAsync(investor, ct);
            _logger.LogInformation("Enrolment {InvestorId} is now {Status}.", investor.Id, investor.Status);
        }
    }

    private async Task ProvisionAccountsAsync(CancellationToken ct)
    {
        var active = await _investors.ListAsync(new InvestorsByStatusSpecification(EnrolmentStatus.Active), ct);
        foreach (var investor in active.Where(i => !i.HasUpvestAccount && !string.IsNullOrEmpty(i.UpvestUserId)))
        {
            try
            {
                var group = await _upvest.CreateAccountGroupAsync(investor.UpvestUserId!, ct);
                var account = await _upvest.CreateAccountAsync(investor.UpvestUserId!, group.Id, ct);
                investor.LinkUpvestAccount(group.Id, account.Id);
                await _investors.UpdateAsync(investor, ct);
                _logger.LogInformation("Provisioned Upvest account for investor {InvestorId}.", investor.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not provision account for investor {InvestorId}: {Error}", investor.Id, ex.Message);
            }
        }
    }

    private async Task InvestDueBalancesAsync(CancellationToken ct)
    {
        var active = await _investors.ListAsync(new InvestorsByStatusSpecification(EnrolmentStatus.Active), ct);
        foreach (var investor in active)
        {
            if (!investor.HasUpvestAccount) continue;
            if (investor.PendingAmount < _settings.InvestmentThreshold) continue;

            // Only one investment in flight per shopper at a time.
            var inFlight = await _investments.CountAsync(new PendingInvestmentsSpecification(investor.Id), ct);
            if (inFlight > 0) continue;

            try
            {
                var account = await _upvest.GetAccountAsync(investor.UpvestAccountId!, ct);
                if (!string.Equals(account.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // account not tradable yet; try again next tick
                }

                var amount = investor.PendingAmount;

                // Fund the account group with the amount being invested, then place the buy order.
                await _upvest.IncreaseVirtualCashAsync(investor.UpvestAccountGroupId!, amount, _settings.Currency, ct);
                var order = await _upvest.PlaceBuyOrderAsync(
                    investor.UpvestUserId!, investor.UpvestAccountId!, _settings.InstrumentId, amount, _settings.Currency, ct);

                var investment = new Investment(investor.BuyerId, investor.Id, amount);
                investment.LinkUpvestOrder(order.Id);
                ApplyOrderStatus(investment, order.Status);
                await _investments.AddAsync(investment, ct);

                investor.ClearPending();
                await _investors.UpdateAsync(investor, ct);
                _logger.LogInformation("Invested {Amount} for investor {InvestorId} (order {OrderId}).",
                    amount, investor.Id, order.Id);
            }
            catch (Exception ex)
            {
                // Leave the balance set aside; the next tick retries.
                _logger.LogWarning("Could not invest balance for investor {InvestorId}: {Error}", investor.Id, ex.Message);
            }
        }
    }

    private async Task SettlePlacedInvestmentsAsync(CancellationToken ct)
    {
        var pending = await _investments.ListAsync(new PendingInvestmentsSpecification(), ct);
        foreach (var investment in pending)
        {
            if (string.IsNullOrEmpty(investment.UpvestOrderId)) continue;
            try
            {
                await ReconcileInvestmentAsync(investment, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not settle investment {InvestmentId}: {Error}", investment.Id, ex.Message);
            }
        }
    }

    private async Task ReconcileInvestmentAsync(Investment investment, CancellationToken ct)
    {
        var order = await _upvest.GetOrderAsync(investment.UpvestOrderId!, ct);
        var before = investment.Status;
        ApplyOrderStatus(investment, order.Status);
        if (investment.Status == before) return;

        if (investment.Status == InvestmentStatus.Failed)
        {
            // Return the money to the set-aside balance so it can be invested again.
            var investor = await _investors.GetByIdAsync(investment.InvestorId, ct);
            if (investor is not null)
            {
                investor.ReturnToPending(investment.Amount);
                await _investors.UpdateAsync(investor, ct);
            }
        }

        await _investments.UpdateAsync(investment, ct);
        _logger.LogInformation("Investment {InvestmentId} is now {Status}.", investment.Id, investment.Status);
    }

    // --- helpers ------------------------------------------------------------------------

    private decimal RoundUp(decimal orderTotal)
    {
        var rounded = decimal.Round(orderTotal, 2, MidpointRounding.AwayFromZero);
        var change = Math.Ceiling(rounded) - rounded;
        return decimal.Round(change, 2, MidpointRounding.AwayFromZero);
    }

    private static void ApplyUserStatus(Investor investor, string? upvestStatus)
    {
        switch ((upvestStatus ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "ACTIVE":
                investor.MarkActive();
                break;
            case "REJECTED":
            case "CANCELLED":
            case "DECLINED":
                investor.MarkRejected();
                break;
            // INACTIVE / PENDING / anything else: still awaiting Upvest's decision.
        }
    }

    private static void ApplyOrderStatus(Investment investment, string? upvestStatus)
    {
        switch ((upvestStatus ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "FILLED":
                investment.MarkSettled();
                break;
            case "CANCELLED":
            case "REJECTED":
            case "EXPIRED":
                investment.MarkFailed();
                break;
            // NEW / PROCESSING: outcome not yet known.
        }
    }
}
