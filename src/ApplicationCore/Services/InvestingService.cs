using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestingService : IInvestingService
{
    private readonly IRepository<Investor> _investors;
    private readonly IUpvestClient _upvest;
    private readonly InvestingOptions _options;
    private readonly IAppLogger<InvestingService> _logger;

    // Investing happens only inside reconciliation, and reconciliation is globally serialised, so a
    // balance that has crossed the threshold is invested exactly once — whether the trigger was the
    // timer or a webhook, and no matter how fast orders arrive.
    private static readonly SemaphoreSlim _reconcileGate = new(1, 1);

    public InvestingService(
        IRepository<Investor> investors,
        IUpvestClient upvest,
        InvestingOptions options,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _upvest = upvest;
        _options = options;
        _logger = logger;
    }

    public async Task<Investor> EnrolAsync(string shopperId, EnrolmentDetails details, CancellationToken cancellationToken)
    {
        var existing = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpec(shopperId), cancellationToken);
        if (existing is not null)
        {
            // Already opted in. Nudge it forward in case a previous attempt stalled, then return.
            await ProgressEnrolmentAsync(existing, cancellationToken);
            return existing;
        }

        var investor = new Investor(shopperId);

        // The shopper becomes an investor with Upvest. This step is required; if it fails the
        // caller is told enrolment did not happen.
        var user = await _upvest.CreateUserAsync(details, cancellationToken);
        investor.LinkUpvestUser(user.Id);
        ApplyUserStatus(investor, user.Status);
        await _investors.AddAsync(investor, cancellationToken);

        // Submit the KYC check and tax residency that let Upvest accept the shopper. Guarded so a
        // transient failure does not abort enrolment; the shopper stays pending and can opt in again.
        try
        {
            await _upvest.CreateKycCheckAsync(user.Id, details.Nationality, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not submit the KYC check for an investor during enrolment.");
        }

        try
        {
            await _upvest.SetTaxResidencyAsync(user.Id, details.TaxCountry, details.TaxId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not record tax residency for an investor during enrolment.");
        }

        await ProgressEnrolmentAsync(investor, cancellationToken);
        return investor;
    }

    public Task<Investor?> GetInvestorAsync(string shopperId, CancellationToken cancellationToken) =>
        _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpec(shopperId), cancellationToken);

    public async Task<long> RecordPaidOrderAsync(string shopperId, decimal orderTotalEuros, CancellationToken cancellationToken)
    {
        // Share the reconcile gate so a set-aside and a reconciliation never read-modify-write the
        // same investor at once (which could otherwise clobber the balance across EF scopes).
        await _reconcileGate.WaitAsync(cancellationToken);
        try
        {
            var investor = await _investors.FirstOrDefaultAsync(new InvestorByShopperIdSpec(shopperId), cancellationToken);

            // Only an accepted investor sets anything aside.
            if (investor is null || investor.Status != EnrolmentStatus.Active)
                return 0;

            var roundUpCents = Money.RoundUpCents(orderTotalEuros);
            if (roundUpCents == 0)
                return 0;

            investor.SetAside(roundUpCents);
            await _investors.UpdateAsync(investor, cancellationToken);

            // The balance is invested by reconciliation (serialised, in its own scope) so a crossed
            // threshold is invested exactly once. Investing deliberately does not happen inline here:
            // the order must never fail because of an investment.
            return roundUpCents;
        }
        finally
        {
            _reconcileGate.Release();
        }
    }

    public async Task ReconcileAllAsync(CancellationToken cancellationToken)
    {
        // Globally serialise reconciliation. Each pass reads investors fresh inside the gate, so a
        // balance is never invested twice even when the timer and a webhook (or an order) coincide.
        await _reconcileGate.WaitAsync(cancellationToken);
        try
        {
            var investors = await _investors.ListAsync(new InvestorsWithInvestmentsSpec(), cancellationToken);
            foreach (var investor in investors)
            {
                try
                {
                    await ReconcileInvestorAsync(investor, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reconciling an investor with Upvest failed.");
                }
            }
        }
        finally
        {
            _reconcileGate.Release();
        }
    }

    private async Task ReconcileInvestorAsync(Investor investor, CancellationToken cancellationToken)
    {
        var changed = false;

        // 1. Progress a pending enrolment towards accepted/rejected.
        if (investor.Status == EnrolmentStatus.Pending && !string.IsNullOrEmpty(investor.UpvestUserId))
        {
            var user = await _upvest.GetUserAsync(investor.UpvestUserId!, cancellationToken);
            if (user is not null)
            {
                ApplyUserStatus(investor, user.Status);
                changed = true;
            }
        }

        // Make sure an accepted investor has somewhere to hold assets.
        if (investor.Status == EnrolmentStatus.Active && !investor.CanInvest)
        {
            await EnsureAccountsAsync(investor, cancellationToken);
            changed = true;
        }

        // 2. Settle in-flight investments against their Upvest orders.
        foreach (var investment in investor.Investments)
        {
            if (investment.Status != InvestmentStatus.Pending || string.IsNullOrEmpty(investment.UpvestOrderId))
                continue;

            var order = await _upvest.GetOrderAsync(investment.UpvestOrderId!, cancellationToken);
            if (order is not null)
            {
                ApplyOrderStatus(investor, investment, order.Status);
                changed = true;
            }
        }

        if (changed)
            await _investors.UpdateAsync(investor, cancellationToken);

        // 3. A balance that has crossed the threshold (e.g. a previous attempt failed) gets invested.
        if (investor.CanInvest && investor.PendingAmountCents >= _options.InvestmentThresholdCents)
            await TryInvestAsync(investor, cancellationToken);
    }

    /// <summary>Move the enrolment as far forward as it can go right now: set up accounts once accepted.</summary>
    private async Task ProgressEnrolmentAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (investor.Status == EnrolmentStatus.Active && !investor.CanInvest)
        {
            await EnsureAccountsAsync(investor, cancellationToken);
            await _investors.UpdateAsync(investor, cancellationToken);
        }
    }

    private async Task TryInvestAsync(Investor investor, CancellationToken cancellationToken)
    {
        // Only ever called from within the serialised reconcile pass.
        if (investor.Status != EnrolmentStatus.Active)
            return;
        if (investor.PendingAmountCents < _options.InvestmentThresholdCents)
            return;

        if (!investor.CanInvest)
        {
            await EnsureAccountsAsync(investor, cancellationToken);
            if (!investor.CanInvest)
                return;
        }

        try
        {
            var amount = investor.PendingAmount; // invest the whole balance

            // Fund the account, then buy the fund. Only once Upvest has accepted the order do we
            // move the money out of the set-aside balance, so a failure never loses it.
            await _upvest.IncreaseVirtualCashAsync(investor.UpvestAccountGroupId!, amount, cancellationToken);
            var order = await _upvest.PlaceOrderAsync(investor.UpvestUserId!, investor.UpvestAccountId!, amount, cancellationToken);

            var investment = investor.BeginInvestment();
            investment.LinkUpvestOrder(order.Id);
            ApplyOrderStatus(investor, investment, order.Status);

            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogInformation("Invested the set-aside balance of an investor (order {OrderId}).", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Investing the set-aside balance for an investor failed; it stays set aside for a later retry.");
        }
    }

    private async Task EnsureAccountsAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (investor.CanInvest || string.IsNullOrEmpty(investor.UpvestUserId))
            return;

        try
        {
            var accountGroupId = investor.UpvestAccountGroupId;
            if (string.IsNullOrEmpty(accountGroupId))
                accountGroupId = await _upvest.CreateAccountGroupAsync(investor.UpvestUserId!, cancellationToken);

            var accountId = await _upvest.CreateAccountAsync(investor.UpvestUserId!, accountGroupId!, cancellationToken);
            investor.LinkUpvestAccounts(accountGroupId!, accountId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set up an investor's Upvest accounts yet; will retry.");
        }
    }

    private static void ApplyUserStatus(Investor investor, string upvestStatus)
    {
        switch (upvestStatus?.ToUpperInvariant())
        {
            case "ACTIVE":
                investor.MarkActive();
                break;
            case "OFFBOARDING":
            case "OFFBOARDED":
                investor.MarkRejected();
                break;
            // INACTIVE / unknown: not yet accepted — leave pending.
        }
    }

    private static void ApplyOrderStatus(Investor investor, Investment investment, string orderStatus)
    {
        switch (orderStatus?.ToUpperInvariant())
        {
            case "FILLED":
                investment.MarkSettled();
                break;
            case "CANCELLED":
                if (investment.Status != InvestmentStatus.Failed)
                {
                    investment.MarkFailed();
                    investor.RefundToPending(investment);
                }
                break;
            // NEW / PROCESSING / unknown: still pending.
        }
    }
}
