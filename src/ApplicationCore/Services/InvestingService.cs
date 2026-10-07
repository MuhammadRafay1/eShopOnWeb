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
/// Orchestrates the shopper's enrolment, round-up ledger and investments, keeping the local state in
/// step with Upvest. All Upvest interaction goes through <see cref="IUpvestInvestingGateway"/>.
/// </summary>
public class InvestingService : IInvestingService
{
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestInvestingGateway _upvest;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(IRepository<Investor> investors, IUpvestInvestingGateway upvest, IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<EnrolmentView> EnrolAsync(string shopperId, InvestorSignup signup, CancellationToken cancellationToken = default)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpec(shopperId), cancellationToken);
        if (existing is not null)
        {
            // Already opted in — enrolment is idempotent per shopper.
            await ReconcileAsync(existing, cancellationToken);
            return ToEnrolmentView(existing);
        }

        var investor = new Investor(shopperId);
        try
        {
            var user = await _upvest.EnrolUserAsync(signup, cancellationToken);
            investor.SetUpvestUser(user.UserId);
            if (user.Status == EnrolmentStatus.Active) investor.MarkActive();
            else if (user.Status == EnrolmentStatus.Rejected) investor.MarkRejected();
        }
        catch (UpvestRejectedException)
        {
            investor.MarkRejected();
        }

        await _investors.AddAsync(investor, cancellationToken);

        // If Upvest accepted the shopper immediately, finish provisioning the account.
        await ReconcileAsync(investor, cancellationToken);
        return ToEnrolmentView(investor);
    }

    public async Task<EnrolmentView?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpec(shopperId), cancellationToken);
        if (investor is null) return null;
        await ReconcileAsync(investor, cancellationToken);
        return ToEnrolmentView(investor);
    }

    public async Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string shopperId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpec(shopperId), cancellationToken);
        if (investor is null) return Array.Empty<InvestmentView>();
        await ReconcileAsync(investor, cancellationToken);
        return investor.Investments
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvestmentView(i.PublicId, decimal.Round(i.Amount, 2), i.Status))
            .ToList();
    }

    public async Task<BalanceView> GetBalanceAsync(string shopperId, CancellationToken cancellationToken = default)
    {
        var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpec(shopperId), cancellationToken);
        if (investor is null) return new BalanceView(0m, 0m);
        await ReconcileAsync(investor, cancellationToken);
        return new BalanceView(decimal.Round(investor.PendingAmount, 2), decimal.Round(investor.InvestedAmount, 2));
    }

    public async Task<decimal> ApplyPaidOrderAsync(string shopperId, decimal orderTotal, CancellationToken cancellationToken = default)
    {
        try
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpec(shopperId), cancellationToken);
            if (investor is null) return 0m; // shopper has not opted in

            // Catch up with Upvest so a just-accepted shopper starts setting aside straight away.
            await ReconcileAsync(investor, cancellationToken);

            if (!investor.CanInvest)
            {
                return 0m; // not an accepted investor — set nothing aside
            }

            var roundUp = investor.SetAsideRoundUp(orderTotal);
            var investment = investor.StartInvestmentIfThresholdReached();
            await _investors.SaveChangesAsync(cancellationToken);

            if (investment is not null)
            {
                await InvestAsync(investor, investment, cancellationToken);
                await _investors.SaveChangesAsync(cancellationToken);
            }

            return roundUp;
        }
        catch (Exception ex)
        {
            // Investing must never break placing an order.
            _logger.LogWarning("Setting change aside failed and was skipped: {Error}", ex.Message);
            return 0m;
        }
    }

    public async Task HandleUpvestEventAsync(IEnumerable<string> upvestUserIds, CancellationToken cancellationToken = default)
    {
        foreach (var userId in upvestUserIds.Where(id => !string.IsNullOrEmpty(id)).Distinct())
        {
            try
            {
                var investor = await _investors.FirstOrDefaultAsync(new InvestorByUpvestUserIdSpec(userId), cancellationToken);
                if (investor is not null)
                {
                    await ReconcileAsync(investor, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Webhook reconciliation skipped for a user: {Error}", ex.Message);
            }
        }
    }

    /// <summary>Places the investment at Upvest, returning the money to the ledger if it cannot be placed.</summary>
    private async Task InvestAsync(Investor investor, Investment investment, CancellationToken cancellationToken)
    {
        try
        {
            if (!investor.HasAccount)
            {
                var account = await _upvest.ProvisionAccountAsync(investor.UpvestUserId!, cancellationToken);
                investor.SetUpvestAccount(account.AccountGroupId, account.AccountId);
            }

            var placed = await _upvest.PlaceInvestmentAsync(investor.UpvestUserId!, investor.UpvestAccountId!, investment.Amount, cancellationToken);
            investment.LinkToUpvestOrder(placed.OrderId);
            if (placed.Status == InvestmentStatus.Settled) investor.SettleInvestment(investment);
            else if (placed.Status == InvestmentStatus.Failed) investor.FailInvestment(investment);
            // otherwise it stays pending until settlement is reconciled
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Investment could not be placed and was returned to the set-aside balance: {Error}", ex.Message);
            investor.FailInvestment(investment);
        }
    }

    /// <summary>
    /// Brings the local record in line with Upvest: accepts a pending investor (provisioning their
    /// account) and resolves the status of any investment still awaiting its outcome. Fully resilient —
    /// any Upvest hiccup leaves the local state unchanged for the next attempt.
    /// </summary>
    private async Task ReconcileAsync(Investor investor, CancellationToken cancellationToken)
    {
        var changed = false;

        if (investor.Status == EnrolmentStatus.Pending && !string.IsNullOrEmpty(investor.UpvestUserId))
        {
            try
            {
                var status = await _upvest.GetUserStatusAsync(investor.UpvestUserId!, cancellationToken);
                if (status == EnrolmentStatus.Active)
                {
                    investor.MarkActive();
                    changed = true;
                    if (!investor.HasAccount)
                    {
                        var account = await _upvest.ProvisionAccountAsync(investor.UpvestUserId!, cancellationToken);
                        investor.SetUpvestAccount(account.AccountGroupId, account.AccountId);
                    }
                }
                else if (status == EnrolmentStatus.Rejected)
                {
                    investor.MarkRejected();
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not reconcile enrolment with Upvest: {Error}", ex.Message);
            }
        }

        foreach (var investment in investor.Investments.Where(i => i.Status == InvestmentStatus.Pending && !string.IsNullOrEmpty(i.UpvestOrderId)))
        {
            try
            {
                var status = await _upvest.GetOrderStatusAsync(investment.UpvestOrderId!, cancellationToken);
                if (status == InvestmentStatus.Settled) { investor.SettleInvestment(investment); changed = true; }
                else if (status == InvestmentStatus.Failed) { investor.FailInvestment(investment); changed = true; }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not reconcile investment with Upvest: {Error}", ex.Message);
            }
        }

        if (changed)
        {
            await _investors.SaveChangesAsync(cancellationToken);
        }
    }

    private static EnrolmentView ToEnrolmentView(Investor investor) => new(investor.EnrolmentId, investor.Status);
}
