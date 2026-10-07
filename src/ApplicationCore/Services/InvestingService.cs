using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestingService : IInvestingService
{
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestInvestingGateway _upvest;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IUpvestInvestingGateway upvest,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<Investor> EnrolAsync(string buyerId, InvestorEnrolmentDetails details, CancellationToken cancellationToken = default)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (existing != null)
        {
            // Already enrolled: report where their acceptance stands rather than enrolling twice.
            return await ReconcileAcceptanceAsync(existing, cancellationToken);
        }

        var result = await _upvest.EnrolAsync(details, cancellationToken);

        var investor = new Investor(buyerId);
        investor.LinkUpvestUser(result.UpvestUserId, result.UpvestAccountId);
        ApplyAcceptance(investor, result.Status);

        await _investors.AddAsync(investor, cancellationToken);
        _logger.LogInformation("Enrolled investor {PublicId} ({Status}).", investor.PublicId, investor.Status);
        return investor;
    }

    public async Task<Investor?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor == null)
        {
            return null;
        }

        return await ReconcileAcceptanceAsync(investor, cancellationToken);
    }

    public async Task<decimal> RecordPaidOrderAsync(string buyerId, int orderId, decimal orderTotal, CancellationToken cancellationToken = default)
    {
        try
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
            if (investor == null)
            {
                return 0m; // shopper has not opted in
            }

            // A shopper who was accepted at Upvest after enrolling only starts accruing once we know.
            if (!investor.IsActive)
            {
                investor = await ReconcileAcceptanceAsync(investor, cancellationToken);
                if (!investor.IsActive)
                {
                    return 0m;
                }
            }

            var roundUp = RoundUp(orderTotal);
            var setAside = investor.SetAside(orderId, roundUp);

            if (investor.IsReadyToInvest)
            {
                await TryInvestAsync(investor, cancellationToken);
            }

            await _investors.UpdateAsync(investor, cancellationToken);
            return setAside;
        }
        catch (Exception ex)
        {
            // Placing an order must never fail because of anything to do with investing.
            _logger.LogWarning("Setting aside change for order {OrderId} failed ({Error}); the order is unaffected.", orderId, ex.GetType().Name);
            return 0m;
        }
    }

    public async Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor == null)
        {
            return Array.Empty<Investment>();
        }

        await ReconcileInvestmentsAsync(investor, cancellationToken);
        return investor.Investments.OrderByDescending(i => i.CreatedAt).ToList();
    }

    public async Task<InvestingBalance> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), cancellationToken);
        if (investor == null)
        {
            return new InvestingBalance(0m, 0m);
        }

        await ReconcileInvestmentsAsync(investor, cancellationToken);
        return new InvestingBalance(investor.PendingAmount, investor.InvestedAmount);
    }

    public async Task ReconcileByUpvestOrderAsync(string upvestOrderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByUpvestOrderSpecification(upvestOrderId), cancellationToken);
            if (investor != null)
            {
                await ReconcileInvestmentsAsync(investor, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Reconciling by Upvest order failed ({Error}).", ex.GetType().Name);
        }
    }

    /// <summary>The amount an order sets aside: the gap between its total and the next whole euro.</summary>
    private static decimal RoundUp(decimal orderTotal)
    {
        var roundUp = Math.Ceiling(orderTotal) - orderTotal;
        return roundUp < 0m ? 0m : roundUp;
    }

    private async Task<Investor> ReconcileAcceptanceAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (investor.Status != InvestorStatus.Pending || string.IsNullOrEmpty(investor.UpvestUserId))
        {
            return investor;
        }

        try
        {
            var result = await _upvest.RefreshAcceptanceAsync(investor.UpvestUserId!, investor.UpvestAccountId, cancellationToken);
            if (!string.IsNullOrEmpty(result.UpvestAccountId) && string.IsNullOrEmpty(investor.UpvestAccountId))
            {
                investor.SetUpvestAccount(result.UpvestAccountId!);
            }

            if (ApplyAcceptance(investor, result.Status))
            {
                await _investors.UpdateAsync(investor, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not refresh acceptance for investor {PublicId} ({Error}).", investor.PublicId, ex.GetType().Name);
        }

        return investor;
    }

    private async Task ReconcileInvestmentsAsync(Investor investor, CancellationToken cancellationToken)
    {
        var pending = investor.Investments.Where(i => i.Status == InvestmentStatus.Pending).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        var changed = false;
        foreach (var investment in pending)
        {
            try
            {
                var state = await _upvest.GetInvestmentStateAsync(investment.UpvestOrderId, cancellationToken);
                switch (state)
                {
                    case UpvestInvestmentState.Settled:
                        investment.MarkSettled();
                        changed = true;
                        break;
                    case UpvestInvestmentState.Failed:
                        investment.MarkFailed();
                        changed = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not reconcile investment {PublicId} ({Error}).", investment.PublicId, ex.GetType().Name);
            }
        }

        if (changed)
        {
            await _investors.UpdateAsync(investor, cancellationToken);
        }
    }

    private async Task TryInvestAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(investor.UpvestUserId) || string.IsNullOrEmpty(investor.UpvestAccountId))
        {
            _logger.LogWarning("Investor {PublicId} is ready to invest but has no Upvest account yet.", investor.PublicId);
            return;
        }

        try
        {
            var result = await _upvest.PlaceInvestmentAsync(
                investor.UpvestUserId!, investor.UpvestAccountId!, investor.PendingAmount, cancellationToken);

            var investment = investor.BeginInvestment(result.InstrumentId, result.UpvestOrderId);
            if (result.State == UpvestInvestmentState.Settled)
            {
                investment.MarkSettled();
            }
            else if (result.State == UpvestInvestmentState.Failed)
            {
                investment.MarkFailed();
            }

            _logger.LogInformation("Invested {Amount} for investor {PublicId} (order {InvestmentId}).",
                investment.Amount, investor.PublicId, investment.PublicId);
        }
        catch (Exception ex)
        {
            // Keep the accrued balance so the next order retries; never surface to the order flow.
            _logger.LogWarning("Investing the accrued balance for investor {PublicId} failed ({Error}); balance retained.", investor.PublicId, ex.GetType().Name);
        }
    }

    /// <summary>Applies an acceptance status from Upvest; returns true when the status changed.</summary>
    private static bool ApplyAcceptance(Investor investor, UpvestAcceptanceStatus status)
    {
        switch (status)
        {
            case UpvestAcceptanceStatus.Active when investor.Status != InvestorStatus.Active:
                investor.MarkActive();
                return true;
            case UpvestAcceptanceStatus.Rejected when investor.Status != InvestorStatus.Rejected:
                investor.MarkRejected();
                return true;
            default:
                return false;
        }
    }
}
