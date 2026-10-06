using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Orchestrates enrolment, the set-aside ledger, investing, and the shopper's views. All work is scoped to
/// a shop identity so one shopper never sees another's data. A per-shopper lock serialises the
/// check-and-invest so the €10 threshold can only trigger one investment at a time.
/// </summary>
public sealed class InvestingService : IInvestingService
{
    /// <summary>Set-aside balance (in cents) at which the whole balance is invested.</summary>
    public const long InvestThresholdCents = 1000;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestInvestorGateway _gateway;
    private readonly ILogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IRepository<Investment> investments,
        IUpvestInvestorGateway gateway,
        ILogger<InvestingService> logger)
    {
        _investors = investors;
        _investments = investments;
        _gateway = gateway;
        _logger = logger;
    }

    private static SemaphoreSlim LockFor(string buyerId) => Locks.GetOrAdd(buyerId, _ => new SemaphoreSlim(1, 1));

    public async Task<EnrolmentView> EnrolAsync(
        string buyerId, InvestorEnrolmentDetails details, CancellationToken cancellationToken)
    {
        var gate = LockFor(buyerId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpec(buyerId), cancellationToken);
            if (existing is not null)
                return new EnrolmentView(existing.Id, existing.Status);

            // Provider provisioning uses idempotency keys derived from the shopper id, so this is safe to
            // retry; the local row is only written once provisioning returns.
            var provisioned = await _gateway.ProvisionInvestorAsync(buyerId, details, cancellationToken);

            var investor = new Investor(buyerId);
            investor.LinkUpvest(provisioned.UpvestUserId, provisioned.AccountGroupId, provisioned.AccountId);
            investor.SetStatus(provisioned.Status);
            await _investors.AddAsync(investor, cancellationToken);

            _logger.LogInformation("Shopper enrolled (investor {Id}, status {Status}).", investor.Id, investor.Status);
            return new EnrolmentView(investor.Id, investor.Status);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpec(buyerId), cancellationToken);
        if (investor is null) return null;

        if (investor.Status == EnrolmentStatus.Pending && investor.UpvestAccountId is { } accountId)
        {
            try
            {
                var latest = await _gateway.GetEnrolmentStatusAsync(accountId, cancellationToken);
                if (latest != investor.Status)
                {
                    investor.SetStatus(latest);
                    await _investors.UpdateAsync(investor, cancellationToken);
                }
            }
            catch (UpvestProviderException ex)
            {
                _logger.LogWarning("Enrolment reconcile skipped: {Message}", ex.Message);
            }
        }

        return new EnrolmentView(investor.Id, investor.Status);
    }

    public async Task<BalanceView> GetBalanceAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpec(buyerId), cancellationToken);
        if (investor is null) return new BalanceView(0, 0);

        var investments = await _investments.ListAsync(new InvestmentsByInvestorSpec(investor.Id), cancellationToken);
        var invested = investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.AmountCents);
        return new BalanceView(investor.PendingAmountCents, invested);
    }

    public async Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(
        string buyerId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpec(buyerId), cancellationToken);
        if (investor is null) return Array.Empty<InvestmentView>();

        var investments = await _investments.ListAsync(new InvestmentsByInvestorSpec(investor.Id), cancellationToken);
        return investments.Select(i => new InvestmentView(i.Id, i.AmountCents, i.Status)).ToList();
    }

    public async Task<long> OnOrderPaidAsync(string buyerId, decimal orderTotalEuros, CancellationToken cancellationToken)
    {
        // Investing must never fail an order: everything here is isolated and swallows its own errors.
        long roundUpCents;
        try
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpec(buyerId), cancellationToken);
            if (investor is null || investor.Status != EnrolmentStatus.Active)
                return 0; // only accepted investors set anything aside

            roundUpCents = RoundUpCents(orderTotalEuros);
            if (roundUpCents == 0) return 0;

            var gate = LockFor(buyerId);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpec(buyerId), cancellationToken);
                if (investor is null) return 0;

                investor.SetAside(roundUpCents);
                await _investors.UpdateAsync(investor, cancellationToken);

                await TryInvestAsync(investor, cancellationToken);
            }
            finally
            {
                gate.Release();
            }

            return roundUpCents;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Setting aside change failed for an order; the order is unaffected.");
            return 0;
        }
    }

    /// <summary>Invests the whole set-aside balance once it reaches the threshold. Caller holds the lock.</summary>
    private async Task TryInvestAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (investor.UpvestUserId is not { } userId || investor.UpvestAccountId is not { } accountId)
            return;

        var amount = investor.WithdrawForInvestment(InvestThresholdCents);
        if (amount == 0) return;

        // Record the investment (and commit the withdrawal) before the provider call, so the money is
        // accounted for and the investment can be reconciled if the call's outcome is unknown.
        var investment = new Investment(investor.Id, amount);
        await _investments.AddAsync(investment, cancellationToken);
        await _investors.UpdateAsync(investor, cancellationToken);

        try
        {
            var result = await _gateway.PlaceInvestmentOrderAsync(
                userId, accountId, amount, investment.Reference, cancellationToken);
            investment.AttachOrder(result.OrderId);
            ApplyStatus(investment, result.Status);
            await _investments.UpdateAsync(investment, cancellationToken);
            _logger.LogInformation("Invested {Cents}c for investor {Investor} (investment {Investment}).",
                amount, investor.Id, investment.Id);
        }
        catch (UpvestProviderException ex) when (!ex.OutcomeUnknown)
        {
            // Definite rejection: the order was not placed. Roll back so the balance accrues again.
            investor.Refund(amount);
            await _investors.UpdateAsync(investor, cancellationToken);
            await _investments.DeleteAsync(investment, cancellationToken);
            _logger.LogWarning("Investment rejected by provider; refunded {Cents}c to the ledger.", amount);
        }
        catch (UpvestProviderException ex)
        {
            // Unknown outcome: leave the investment Pending; the reconciler settles it by its reference.
            _logger.LogWarning("Investment outcome unknown ({Message}); left pending for reconciliation.", ex.Message);
        }
    }

    private static void ApplyStatus(Investment investment, InvestmentStatus status)
    {
        switch (status)
        {
            case InvestmentStatus.Settled: investment.MarkSettled(); break;
            case InvestmentStatus.Failed: investment.MarkFailed(); break;
        }
    }

    /// <summary>Cents needed to round a euro total up to the next whole euro (0 if already whole).</summary>
    public static long RoundUpCents(decimal totalEuros)
    {
        var totalCents = (long)Math.Round(totalEuros * 100m, MidpointRounding.AwayFromZero);
        var remainder = totalCents % 100;
        return remainder == 0 ? 0 : 100 - remainder;
    }
}
