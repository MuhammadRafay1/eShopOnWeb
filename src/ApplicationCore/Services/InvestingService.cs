using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestingService : IInvestingService
{
    private static readonly string[] SettledStatuses = { "FILLED", "SETTLED" };
    private static readonly string[] FailedStatuses = { "CANCELLED", "REJECTED", "EXPIRED", "FAILED" };

    // Single-host guard: serialise the set-aside + invest-trigger per shopper so a balance crossing €10
    // starts exactly one investment even under concurrent orders. (In-memory store; one process.)
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    private readonly IRepository<Investor> _investors;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestGateway _upvest;
    private readonly IEnrolmentReconciler _reconciler;
    private readonly IInvestmentQueue _queue;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IRepository<Investment> investments,
        IUpvestGateway upvest,
        IEnrolmentReconciler reconciler,
        IInvestmentQueue queue,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _investments = investments;
        _upvest = upvest;
        _reconciler = reconciler;
        _queue = queue;
        _logger = logger;
    }

    public async Task<EnrolmentView> EnrolAsync(string buyerId, InvestorEnrolmentForm form, CancellationToken cancellationToken)
    {
        var gate = Locks.GetOrAdd(buyerId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
            if (existing is not null)
            {
                await _reconciler.ReconcileAsync(existing, cancellationToken);
                return ToView(existing);
            }

            var result = await _upvest.EnrolAsync(form, cancellationToken);
            var investor = new Investor(buyerId);
            investor.LinkUpvestUser(result.UpvestUserId);
            await _investors.AddAsync(investor, cancellationToken);
            _logger.LogInformation("Investor {InvestorId} enrolled with Upvest.", investor.Id);
            return ToView(investor);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
        {
            return null;
        }
        await _reconciler.ReconcileAsync(investor, cancellationToken);
        return ToView(investor);
    }

    public async Task<BalanceView?> GetBalanceAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
        {
            return null;
        }
        await _reconciler.ReconcileAsync(investor, cancellationToken);
        await ReconcileInvestmentsAsync(investor, cancellationToken);
        return new BalanceView(Money.ToEuros(investor.PendingAmountCents), Money.ToEuros(investor.InvestedAmountCents));
    }

    public async Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor is null)
        {
            return Array.Empty<InvestmentView>();
        }
        await _reconciler.ReconcileAsync(investor, cancellationToken);
        await ReconcileInvestmentsAsync(investor, cancellationToken);

        var investments = await _investments.ListAsync(new InvestmentsByInvestorSpecification(investor.Id), cancellationToken);
        return investments
            .Select(i => new InvestmentView(i.Id, Money.ToEuros(i.AmountCents), ToStatusString(i.Status)))
            .ToList();
    }

    public async Task<long> HandleOrderPaidAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken)
    {
        // Investing must never make placing an order fail: swallow everything here.
        try
        {
            var roundUp = Money.RoundUpCents(orderTotal);
            if (roundUp == 0)
            {
                return 0;
            }

            var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
            if (investor is null)
            {
                return 0;
            }

            await _reconciler.ReconcileAsync(investor, cancellationToken);

            var gate = Locks.GetOrAdd(buyerId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                // Re-read fresh state inside the lock before deciding.
                investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
                if (investor is null || investor.Status != EnrolmentStatus.Active)
                {
                    return 0;
                }

                investor.AddSetAside(roundUp);
                await _investors.UpdateAsync(investor, cancellationToken);
                _logger.LogInformation("Investor {InvestorId} set aside {Cents} cents (pending now {Pending}).",
                    investor.Id, roundUp, investor.PendingAmountCents);

                if (investor.CanInvest)
                {
                    var amount = investor.TakePendingForInvestment();
                    var investment = new Investment(investor.Id, amount);
                    await _investments.AddAsync(investment, cancellationToken);
                    await _investors.UpdateAsync(investor, cancellationToken);
                    _queue.Enqueue(investment.Id);
                    _logger.LogInformation("Investor {InvestorId} started investment {InvestmentId} of {Cents} cents.",
                        investor.Id, investment.Id, amount);
                }

                return roundUp;
            }
            finally
            {
                gate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Set-aside for shopper failed but the order still stands: {Reason}", ex.Message);
            return 0;
        }
    }

    /// <summary>Pull the latest outcome of each pending investment from Upvest and settle it locally.</summary>
    private async Task ReconcileInvestmentsAsync(Investor investor, CancellationToken cancellationToken)
    {
        var pending = await _investments.ListAsync(new PendingInvestmentsSpecification(investor.Id), cancellationToken);
        foreach (var investment in pending)
        {
            try
            {
                UpvestOrderState? state = null;
                if (investment.UpvestOrderId is not null)
                {
                    state = await _upvest.GetOrderAsync(investment.UpvestOrderId, cancellationToken);
                }
                else if (investor.AccountId is not null)
                {
                    // Uncertain write: the order may have been placed without us recording its id.
                    state = await _upvest.FindOrderByReferenceAsync(investor.AccountId, investment.ClientReference, cancellationToken);
                    if (state is not null)
                    {
                        investment.LinkOrder(state.UpvestOrderId);
                    }
                }

                if (state is null)
                {
                    continue;
                }

                if (Array.Exists(SettledStatuses, s => s.Equals(state.Status, StringComparison.OrdinalIgnoreCase)))
                {
                    investment.MarkSettled();
                    investor.RecordInvested(investment.AmountCents);
                    await _investments.UpdateAsync(investment, cancellationToken);
                    await _investors.UpdateAsync(investor, cancellationToken);
                }
                else if (Array.Exists(FailedStatuses, s => s.Equals(state.Status, StringComparison.OrdinalIgnoreCase)))
                {
                    investment.MarkFailed();
                    investor.ReturnFailedInvestment(investment.AmountCents);
                    await _investments.UpdateAsync(investment, cancellationToken);
                    await _investors.UpdateAsync(investor, cancellationToken);
                }
            }
            catch (UpvestException ex)
            {
                _logger.LogWarning("Could not reconcile investment {InvestmentId}: {Reason}", investment.Id, ex.Message);
            }
        }
    }

    private static EnrolmentView ToView(Investor investor) => new(investor.Id, ToStatusString(investor.Status));

    private static string ToStatusString(EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending"
    };

    private static string ToStatusString(InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending"
    };
}
