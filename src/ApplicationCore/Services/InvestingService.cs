using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Orchestrates the "invest your change" capability over the <see cref="Investor"/> and
/// <see cref="Investment"/> aggregates and the <see cref="IUpvestInvestorGateway"/>. Setting aside
/// change touches only local state; talking to Upvest to invest and to settle happens in
/// <see cref="ReconcileAsync"/>, off the shopper's request path.
/// </summary>
public class InvestingService : IInvestingService
{
    /// <summary>Change accrues until it reaches this many euros, then the whole balance is invested.</summary>
    public const decimal InvestmentThreshold = 10m;

    // Ensures only one reconciliation pass runs at a time (the timer and a webhook can both trigger one),
    // which keeps a matured balance from being invested twice.
    private static readonly SemaphoreSlim ReconcileGate = new(1, 1);

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestInvestorGateway _gateway;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IRepository<Investment> investments,
        IUpvestInvestorGateway gateway,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _investments = investments;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<Investor> EnrolAsync(string buyerId, InvestorSignUpForm form, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(form, nameof(form));

        var existing = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (existing is not null)
        {
            // Opting in is idempotent per shopper; return the enrolment already in flight.
            return existing;
        }

        // Onboard with Upvest first; only persist an enrolment once Upvest has accepted the submission,
        // so a failure leaves nothing behind and the shopper can simply try again.
        var result = await _gateway.EnrolInvestorAsync(form, cancellationToken);

        var investor = new Investor(buyerId);
        investor.SetUpvestUser(result.UserId, result.KycCheckId);
        await _investors.AddAsync(investor, cancellationToken);

        _logger.LogInformation("Investor enrolment submitted for buyer {BuyerId}; awaiting Upvest acceptance.", buyerId);
        return investor;
    }

    public Task<Investor?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken) =>
        _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);

    public async Task<decimal> SetAsideFromPaidOrderAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken)
    {
        // This runs on the order request path and must never make placing an order fail.
        try
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
            if (investor is null || investor.Status != EnrolmentStatus.Active)
            {
                return 0m;
            }

            var roundUp = RoundUpToNextEuro(orderTotal);
            if (roundUp <= 0m)
            {
                return 0m;
            }

            investor.SetAside(roundUp);
            await _investors.UpdateAsync(investor, cancellationToken);
            return roundUp;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Setting aside change for buyer {BuyerId} failed; the order is unaffected: {Error}", buyerId, ex.Message);
            return 0m;
        }
    }

    public async Task<InvestorBalance> GetBalanceAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        var pending = investor?.PendingAmount ?? 0m;

        var investments = await _investments.ListAsync(new InvestmentsByBuyerIdSpecification(buyerId), cancellationToken);
        var invested = investments
            .Where(i => i.Status != InvestmentStatus.Failed)
            .Sum(i => i.Amount);

        return new InvestorBalance(
            decimal.Round(pending, 2, MidpointRounding.AwayFromZero),
            decimal.Round(invested, 2, MidpointRounding.AwayFromZero));
    }

    public async Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investments = await _investments.ListAsync(new InvestmentsByBuyerIdSpecification(buyerId), cancellationToken);
        return investments.ToList();
    }

    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        if (!await ReconcileGate.WaitAsync(0, cancellationToken))
        {
            // A pass is already running; let it finish rather than overlapping.
            return;
        }

        try
        {
            await _gateway.EnsureWebhookSubscriptionAsync(cancellationToken);

            await AdvancePendingEnrolmentsAsync(cancellationToken);
            await InvestMaturedBalancesAsync(cancellationToken);
            await SettlePendingInvestmentsAsync(cancellationToken);
        }
        finally
        {
            ReconcileGate.Release();
        }
    }

    private async Task AdvancePendingEnrolmentsAsync(CancellationToken cancellationToken)
    {
        var pending = await _investors.ListAsync(new PendingInvestorsSpecification(), cancellationToken);
        foreach (var investor in pending)
        {
            try
            {
                var status = await _gateway.GetEnrolmentStatusAsync(
                    investor.UpvestUserId!.Value, investor.UpvestKycCheckId!.Value, cancellationToken);

                if (status == EnrolmentStatus.Rejected)
                {
                    investor.SetStatus(EnrolmentStatus.Rejected);
                    await _investors.UpdateAsync(investor, cancellationToken);
                    _logger.LogInformation("Enrolment for buyer {BuyerId} was rejected by Upvest.", investor.BuyerId);
                    continue;
                }

                if (status == EnrolmentStatus.Active)
                {
                    // The user is accepted. Provision the account group and trading account (once), then
                    // mark the shopper active so their change can be invested.
                    if (investor.UpvestAccountId is null)
                    {
                        var accounts = await _gateway.ProvisionAccountsAsync(investor.UpvestUserId!.Value, cancellationToken);
                        investor.SetUpvestAccounts(accounts.AccountGroupId, accounts.AccountId);
                    }

                    investor.SetStatus(EnrolmentStatus.Active);
                    await _investors.UpdateAsync(investor, cancellationToken);
                    _logger.LogInformation("Enrolment for buyer {BuyerId} is now active.", investor.BuyerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not advance enrolment for buyer {BuyerId}: {Error}", investor.BuyerId, ex.Message);
            }
        }
    }

    private async Task InvestMaturedBalancesAsync(CancellationToken cancellationToken)
    {
        var investable = await _investors.ListAsync(new InvestableInvestorsSpecification(InvestmentThreshold), cancellationToken);
        foreach (var investor in investable)
        {
            try
            {
                await InvestAsync(investor, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Investing the set-aside balance for buyer {BuyerId} failed; it will be retried: {Error}", investor.BuyerId, ex.Message);
            }
        }
    }

    private async Task InvestAsync(Investor investor, CancellationToken cancellationToken)
    {
        var amount = investor.PendingAmount;
        if (amount < InvestmentThreshold || !investor.CanInvest)
        {
            return;
        }

        var orderId = await _gateway.InvestAsync(
            investor.UpvestAccountGroupId!.Value,
            investor.UpvestAccountId!.Value,
            investor.UpvestUserId!.Value,
            amount,
            cancellationToken);

        // Only record the investment and reduce the balance once Upvest has accepted the order.
        var investment = new Investment(investor.BuyerId, amount);
        investment.SetUpvestOrder(orderId);
        await _investments.AddAsync(investment, cancellationToken);

        investor.DeductPending(amount);
        await _investors.UpdateAsync(investor, cancellationToken);

        _logger.LogInformation("Invested {Amount} EUR of set-aside change for buyer {BuyerId}.", amount, investor.BuyerId);
    }

    private async Task SettlePendingInvestmentsAsync(CancellationToken cancellationToken)
    {
        var open = await _investments.ListAsync(new PendingInvestmentsSpecification(), cancellationToken);
        foreach (var investment in open)
        {
            try
            {
                var status = await _gateway.GetInvestmentStatusAsync(investment.UpvestOrderId!.Value, cancellationToken);
                if (status != investment.Status)
                {
                    investment.SetStatus(status);
                    await _investments.UpdateAsync(investment, cancellationToken);
                    _logger.LogInformation("Investment {InvestmentId} is now {Status}.", investment.InvestmentId, status);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not refresh investment {InvestmentId} status: {Error}", investment.InvestmentId, ex.Message);
            }
        }
    }

    /// <summary>The change set aside by one paid order: the gap up to the next whole euro.</summary>
    public static decimal RoundUpToNextEuro(decimal orderTotal)
    {
        if (orderTotal <= 0m)
        {
            return 0m;
        }

        var roundUp = Math.Ceiling(orderTotal) - orderTotal;
        return decimal.Round(roundUp, 2, MidpointRounding.AwayFromZero);
    }
}
