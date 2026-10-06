using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class InvestingService : IInvestingService
{
    // Upper bound on how long investing may delay a POST /api/orders response; the order has already been
    // placed and the round-up already set aside, so exceeding this just leaves the investment to be
    // reconciled later.
    private static readonly TimeSpan InvestBudget = TimeSpan.FromSeconds(20);

    private readonly IRepository<Investor> _investors;
    private readonly IUpvestGateway _gateway;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Investor> investors,
        IUpvestGateway gateway,
        IAppLogger<InvestingService> logger)
    {
        _investors = investors;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<Investor> EnrolAsync(
        string shopperId, InvestorEnrolmentData data, CancellationToken cancellationToken)
    {
        var existing = await _investors.FirstOrDefaultAsync(
            new InvestorByShopperIdSpecification(shopperId), cancellationToken);
        if (existing is not null)
        {
            // Already opted in — enrolment is idempotent; never register a second time with the provider.
            return existing;
        }

        // Claim the shopper before calling the provider so a concurrent second opt-in cannot register twice.
        var investor = new Investor(shopperId);
        await _investors.AddAsync(investor, cancellationToken);

        try
        {
            var registration = await _gateway.RegisterInvestorAsync(data, cancellationToken);
            investor.RecordUpvestUser(registration.UpvestUserId, registration.WebhookId);
            // The user activates asynchronously; the account is provisioned later (on status refresh), so the
            // enrolment stays pending until the provider has accepted the shopper.
            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogInformation(
                "Investor enrolled: enrolmentId {EnrolmentId}, status {Status}.",
                investor.EnrolmentId, investor.Status);
            return investor;
        }
        catch (UpvestGatewayException)
        {
            // Registration failed — release the claim so the shopper can retry, then surface the failure.
            await _investors.DeleteAsync(investor, cancellationToken);
            throw;
        }
    }

    public async Task<Investor?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(
            new InvestorByShopperIdSpecification(shopperId), cancellationToken);
        if (investor is null)
        {
            return null;
        }

        await RefreshEnrolmentStatusAsync(investor, cancellationToken);
        return investor;
    }

    public async Task<decimal> ApplyPaidOrderAsync(
        string shopperId, decimal orderTotal, CancellationToken cancellationToken)
    {
        try
        {
            var investor = await _investors.FirstOrDefaultAsync(
                new InvestorByShopperIdSpecification(shopperId), cancellationToken);

            // Orders from a shopper who is not an accepted investor set nothing aside.
            if (investor is null || investor.Status != EnrolmentStatus.Active)
            {
                return 0m;
            }

            var roundUp = investor.SetAsideRoundUp(orderTotal);
            await _investors.UpdateAsync(investor, cancellationToken);

            if (investor.IsReadyToInvest)
            {
                // Guaranteed not to throw — investing must never fail the order.
                await TryInvestAsync(investor, cancellationToken);
            }

            return roundUp;
        }
        catch (Exception ex)
        {
            // Placing the order must never fail because of anything to do with investing.
            _logger.LogWarning("Setting aside change failed and was ignored: {Error}.", ex.Message);
            return 0m;
        }
    }

    public async Task<Investor?> GetLedgerAsync(string shopperId, CancellationToken cancellationToken)
    {
        var investor = await _investors.FirstOrDefaultAsync(
            new InvestorByShopperIdSpecification(shopperId), cancellationToken);
        if (investor is null)
        {
            return null;
        }

        await ReconcilePendingInvestmentsAsync(investor, cancellationToken);
        return investor;
    }

    public async Task ReconcileAllAsync(CancellationToken cancellationToken)
    {
        var investors = await _investors.ListAsync(cancellationToken);
        foreach (var investor in investors)
        {
            try
            {
                var changed = false;
                if (investor.Status == EnrolmentStatus.Pending)
                {
                    changed |= await RefreshEnrolmentStatusAsync(investor, cancellationToken, save: false);
                }

                changed |= await ReconcilePendingInvestmentsAsync(investor, cancellationToken, save: false);

                if (changed)
                {
                    await _investors.UpdateAsync(investor, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Reconciliation of an investor failed and was skipped: {Error}.", ex.Message);
            }
        }
    }

    private async Task TryInvestAsync(Investor investor, CancellationToken cancellationToken)
    {
        if (investor.UpvestUserId is not { } userId
            || investor.UpvestAccountGroupId is not { } accountGroupId
            || investor.UpvestAccountId is not { } accountId)
        {
            return;
        }

        Investment investment;
        try
        {
            // Claim: persist the pending investment and the zeroed balance BEFORE calling the provider.
            investment = investor.BeginInvestment();
            await _investors.UpdateAsync(investor, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not begin an investment: {Error}.", ex.Message);
            return;
        }

        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(InvestBudget);
            var placement = await _gateway.PlaceInvestmentAsync(
                new UpvestInvestmentInstruction
                {
                    UpvestUserId = userId,
                    AccountGroupId = accountGroupId,
                    AccountId = accountId,
                    Amount = investment.Amount,
                    ClientReference = investment.ClientReference,
                    IdempotencyKey = investment.IdempotencyKey
                },
                budget.Token);

            investment.RecordUpvestOrder(placement.UpvestOrderId);
            ApplyInvestmentStatus(investor, investment, placement.InitialStatus);
            await _investors.UpdateAsync(investor, cancellationToken);
            _logger.LogInformation(
                "Investment placed: investmentId {InvestmentId}, amount {Amount}, status {Status}.",
                investment.PublicId, investment.Amount, investment.Status);
        }
        catch (UpvestGatewayException ex)
        {
            await SettleFailedPlacementAsync(investor, investment, ex, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Unexpected failure placing an investment (left pending): {Error}.", ex.Message);
        }
    }

    private async Task SettleFailedPlacementAsync(
        Investor investor, Investment investment, UpvestGatewayException ex, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Placing an investment failed (status {Status}, outcomeUnknown {Unknown}).",
            ex.StatusCode?.ToString() ?? "n/a", ex.OutcomeUnknown);

        try
        {
            if (!ex.OutcomeUnknown)
            {
                // Definite failure: return the money to the set-aside balance to accrue toward the next try.
                investor.FailInvestment(investment);
            }

            // Outcome unknown: leave the investment pending; the reconciliation sweep settles it by
            // looking the order up at the provider by its client_reference.
            await _investors.UpdateAsync(investor, cancellationToken);
        }
        catch (Exception saveEx)
        {
            _logger.LogWarning("Could not persist a failed investment: {Error}.", saveEx.Message);
        }
    }

    private async Task<bool> RefreshEnrolmentStatusAsync(
        Investor investor, CancellationToken cancellationToken, bool save = true)
    {
        if (investor.Status != EnrolmentStatus.Pending || investor.UpvestUserId is not { } userId)
        {
            return false;
        }

        try
        {
            EnrolmentStatus status;
            var provisionedNow = false;
            if (!investor.IsAccountProvisioned)
            {
                // Phase two: provision the account once the user is active. The provider returns 409 while
                // the user is still activating, which we treat as "still pending".
                var provision = await _gateway.ProvisionAccountAsync(userId, cancellationToken);
                investor.RecordUpvestAccount(provision.AccountGroupId, provision.AccountId);
                status = provision.Status;
                provisionedNow = true;
                _logger.LogInformation(
                    "Investor account provisioned: enrolmentId {EnrolmentId}.", investor.EnrolmentId);
            }
            else
            {
                status = await _gateway.GetAccountStatusAsync(investor.UpvestAccountId!.Value, cancellationToken);
            }

            var statusChanged = ApplyEnrolmentStatus(investor, status);

            // Persist newly captured account ids even when the status itself has not changed yet.
            if ((provisionedNow || statusChanged) && save)
            {
                await _investors.UpdateAsync(investor, cancellationToken);
            }

            return provisionedNow || statusChanged;
        }
        catch (UpvestGatewayException ex)
        {
            if (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // The user is not active yet — remain pending and try again on the next read.
                return false;
            }

            // A read must not fail because the provider is momentarily unavailable.
            _logger.LogWarning("Could not refresh enrolment status (status {Status}).", ex.StatusCode?.ToString() ?? "n/a");
            return false;
        }
    }

    private static bool ApplyEnrolmentStatus(Investor investor, EnrolmentStatus status)
    {
        switch (status)
        {
            case EnrolmentStatus.Active when investor.Status != EnrolmentStatus.Active:
                investor.MarkActive();
                return true;
            case EnrolmentStatus.Rejected when investor.Status != EnrolmentStatus.Rejected:
                investor.MarkRejected();
                return true;
            default:
                return false;
        }
    }

    private async Task<bool> ReconcilePendingInvestmentsAsync(
        Investor investor, CancellationToken cancellationToken, bool save = true)
    {
        var pending = investor.Investments.Where(i => i.Status == InvestmentStatus.Pending).ToList();
        if (pending.Count == 0)
        {
            return false;
        }

        var changed = false;
        foreach (var investment in pending)
        {
            try
            {
                if (investment.UpvestOrderId is { } orderId)
                {
                    var status = await _gateway.GetInvestmentStatusAsync(orderId, cancellationToken);
                    if (status != InvestmentStatus.Pending)
                    {
                        ApplyInvestmentStatus(investor, investment, status);
                        changed = true;
                    }
                }
                else if (investor.UpvestAccountId is { } accountId)
                {
                    // Unknown-outcome placement: find the order by the reference we sent.
                    var placement = await _gateway.FindInvestmentByReferenceAsync(
                        accountId, investment.ClientReference, cancellationToken);
                    if (placement is not null)
                    {
                        investment.RecordUpvestOrder(placement.UpvestOrderId);
                        ApplyInvestmentStatus(investor, investment, placement.InitialStatus);
                        changed = true;
                    }
                }
            }
            catch (UpvestGatewayException ex)
            {
                _logger.LogWarning("Could not reconcile an investment (status {Status}).", ex.StatusCode?.ToString() ?? "n/a");
            }
        }

        if (changed && save)
        {
            await _investors.UpdateAsync(investor, cancellationToken);
        }

        return changed;
    }

    private static void ApplyInvestmentStatus(Investor investor, Investment investment, InvestmentStatus status)
    {
        switch (status)
        {
            case InvestmentStatus.Settled:
                investment.MarkSettled();
                break;
            case InvestmentStatus.Failed:
                investor.FailInvestment(investment);
                break;
        }
    }
}
