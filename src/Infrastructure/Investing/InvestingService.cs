using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Orchestrates enrolment and the change ledger. Enrolment creation is idempotent per shopper (a claim row
/// is inserted before the Upvest user is created, so a second concurrent opt-in gets the same enrolment).
/// Onboarding past user-creation, and all investing, are driven by <see cref="UpvestReconciliationService"/>.
/// </summary>
public sealed class InvestingService : IInvestingService
{
    private readonly IRepository<Enrolment> _enrolments;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestGateway _gateway;
    private readonly InvestingCoordinator _coordinator;
    private readonly ILogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Enrolment> enrolments,
        IRepository<Investment> investments,
        IUpvestGateway gateway,
        InvestingCoordinator coordinator,
        ILogger<InvestingService> logger)
    {
        _enrolments = enrolments;
        _investments = investments;
        _gateway = gateway;
        _coordinator = coordinator;
        _logger = logger;
    }

    public async Task<EnrolmentView> EnrolAsync(string buyerId, InvestorSignupForm form, CancellationToken cancellationToken)
    {
        // Claim: insert the enrolment row before creating the Upvest user, so a concurrent second opt-in
        // from the same shopper finds it and does not create a second investor.
        var (enrolment, isNew) = await _coordinator.RunLockedAsync(async () =>
        {
            var existing = await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerIdSpec(buyerId), cancellationToken);
            if (existing is not null)
                return (existing, false);

            var created = new Enrolment(buyerId, form.TaxCountry, form.TaxId);
            await _enrolments.AddAsync(created, cancellationToken);
            return (created, true);
        }, cancellationToken);

        // The user is created here (it needs the full form). A re-POST of a claim that never got a user
        // (e.g. a transient failure first time) retries it; anything else just returns what exists.
        var needsUser = isNew || (enrolment.UpvestUserId is null && enrolment.Status == EnrolmentStatus.Pending);
        if (!needsUser)
            return ToView(enrolment);

        try
        {
            var upvestUserId = await _gateway.CreateInvestorAsync(form, enrolment.CreateUserIdempotencyKey, cancellationToken);
            await _coordinator.RunLockedAsync(async () =>
            {
                enrolment.RecordUser(upvestUserId);
                await _enrolments.UpdateAsync(enrolment, cancellationToken);
            }, cancellationToken);
            _logger.LogInformation("Enrolment {EnrolmentId} created an Upvest investor.", enrolment.Id);
        }
        catch (UpvestApiException ex)
        {
            // A rejection from Upvest (or an unknown outcome we could not confirm) leaves the shopper
            // un-enrolled; the reconciliation worker will retry user creation while the enrolment still
            // has no user id, unless Upvest definitively rejected it (a 4xx).
            if (ex is { OutcomeUnknown: false, StatusCode: >= 400 and < 500 })
            {
                await _coordinator.RunLockedAsync(async () =>
                {
                    enrolment.Reject($"Upvest declined the investor (HTTP {ex.StatusCode}).");
                    await _enrolments.UpdateAsync(enrolment, cancellationToken);
                }, cancellationToken);
                _logger.LogWarning("Enrolment {EnrolmentId} was rejected by Upvest (HTTP {Status}).", enrolment.Id, ex.StatusCode);
            }
            else
            {
                _logger.LogWarning(ex, "Enrolment {EnrolmentId} could not create an Upvest investor yet; will retry.", enrolment.Id);
            }
        }

        return ToView(enrolment);
    }

    public async Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken)
    {
        var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerIdSpec(buyerId), cancellationToken);
        return enrolment is null ? null : ToView(enrolment);
    }

    public async Task<decimal> RecordPaidOrderAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken)
    {
        // Placing an order must never fail because of investing — swallow everything and set aside nothing.
        try
        {
            var roundUpCents = ChangeMath.RoundUpCents(orderTotal);
            if (roundUpCents == 0)
                return 0m;

            return await _coordinator.RunLockedAsync(async () =>
            {
                var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerIdSpec(buyerId), cancellationToken);
                if (enrolment is null || !enrolment.CanInvest)
                    return 0m;

                enrolment.AddSetAside(roundUpCents);
                await _enrolments.UpdateAsync(enrolment, cancellationToken);
                return roundUpCents / 100m;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Setting aside change for a paid order failed; the order is unaffected.");
            return 0m;
        }
    }

    public async Task<BalanceView?> GetBalanceAsync(string buyerId, CancellationToken cancellationToken)
    {
        var enrolment = await _enrolments.FirstOrDefaultAsync(new EnrolmentByBuyerIdSpec(buyerId), cancellationToken);
        return enrolment is null ? null : new BalanceView(enrolment.PendingCents, enrolment.InvestedCents);
    }

    public async Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken)
    {
        var investments = await _investments.ListAsync(new InvestmentsByBuyerIdSpec(buyerId), cancellationToken);
        return investments.Select(i => new InvestmentView(i.Id, i.AmountCents, StatusString(i.Status))).ToList();
    }

    private static EnrolmentView ToView(Enrolment enrolment) => new(enrolment.Id, StatusString(enrolment.Status));

    private static string StatusString(EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Active => "active",
        EnrolmentStatus.Rejected => "rejected",
        _ => "pending",
    };

    private static string StatusString(InvestmentStatus status) => status switch
    {
        InvestmentStatus.Settled => "settled",
        InvestmentStatus.Failed => "failed",
        _ => "pending",
    };
}
