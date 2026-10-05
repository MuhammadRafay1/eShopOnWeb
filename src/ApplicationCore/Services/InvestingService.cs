using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// The request-facing operations of "invest your change": opting in, setting aside the round-up on a
/// paid order, and reporting the shopper's balance and investments. Progression of enrolment and
/// investments at Upvest happens in <see cref="InvestingReconciler"/>, off the request path.
/// </summary>
public class InvestingService
{
    private readonly IRepository<Enrolment> _enrolments;
    private readonly IRepository<Investment> _investments;
    private readonly IUpvestGateway _upvest;
    private readonly IAppLogger<InvestingService> _logger;

    public InvestingService(
        IRepository<Enrolment> enrolments,
        IRepository<Investment> investments,
        IUpvestGateway upvest,
        IAppLogger<InvestingService> logger)
    {
        _enrolments = enrolments;
        _investments = investments;
        _upvest = upvest;
        _logger = logger;
    }

    public Task<Enrolment?> GetEnrolmentAsync(string shopperId, CancellationToken ct) =>
        _enrolments.FirstOrDefaultAsync(new EnrolmentByShopperSpecification(shopperId), ct);

    /// <summary>
    /// Opt the shopper in. Creates the investor, KYC check and tax residency at Upvest, then persists the
    /// enrolment as pending; a background reconciler carries it to active once Upvest accepts the shopper.
    /// Idempotent: a repeat call returns the existing enrolment.
    /// </summary>
    public async Task<Enrolment> EnrolAsync(string shopperId, UpvestInvestorDetails details, CancellationToken ct)
    {
        var existing = await GetEnrolmentAsync(shopperId, ct);
        if (existing is not null)
        {
            // Terminal, or creation already completed (user recorded) — return as-is; progression continues
            // in the reconciler. Only an incomplete row (no Upvest user yet) is retried below.
            if (existing.Stage == EnrolmentStage.Rejected || existing.UpvestUserId is not null)
                return existing;
        }

        // Claim first: persist the row (PK = shopper id) BEFORE calling Upvest, so a concurrent second
        // opt-in is refused by the store and cannot create a duplicate investor.
        var enrolment = existing;
        if (enrolment is null)
        {
            enrolment = new Enrolment(shopperId);
            try
            {
                await _enrolments.AddAsync(enrolment, ct);
            }
            catch (Exception) // concurrent opt-in won the primary-key race; use theirs
            {
                var winner = await GetEnrolmentAsync(shopperId, ct);
                if (winner is null) throw;
                if (winner.Stage == EnrolmentStage.Rejected || winner.UpvestUserId is not null)
                    return winner;
                enrolment = winner;
            }
        }

        // A stable consent timestamp keeps the create-user body byte-identical across retries so the
        // idempotency key replays rather than tripping a changed-body rejection.
        var stableDetails = details with { ConsentTimestamp = enrolment.ConsentTimestamp };

        try
        {
            var userId = await _upvest.CreateInvestorAsync(stableDetails, enrolment.CreateUserIdempotencyKey, ct);
            await _upvest.SubmitKycCheckAsync(userId, ct);
            await _upvest.SetTaxResidencyAsync(userId, stableDetails.TaxCountry, stableDetails.TaxId, enrolment.SetTaxIdempotencyKey, ct);

            // Record the user only after all three succeed, so a partial failure leaves the row retriable.
            enrolment.RecordUser(userId);
            await _enrolments.UpdateAsync(enrolment, ct);
            _logger.LogInformation("Enrolment created and pending Upvest activation.");
        }
        catch (UpvestGatewayException ex) when (!ex.IsTransient)
        {
            // Upvest will not take the shopper on (e.g. a 4xx on the sign-up data).
            _logger.LogWarning($"Enrolment rejected by Upvest (status {ex.StatusCode}).");
            enrolment.MarkRejected($"Upvest rejected enrolment (status {ex.StatusCode}).");
            await _enrolments.UpdateAsync(enrolment, ct);
        }
        // Transient failures propagate: the row stays retriable (no Upvest user recorded) and the caller
        // can retry the opt-in, which replays the same idempotent writes.

        return await GetEnrolmentAsync(shopperId, ct) ?? enrolment;
    }

    /// <summary>
    /// Record a paid order for the shopper and set aside its round-up to the next whole euro. Returns the
    /// amount set aside in cents (0 when the shopper is not an accepted investor or the total was whole).
    /// When the set-aside balance reaches €10 it is turned into a pending investment.
    /// </summary>
    public async Task<long> RecordPaidOrderAsync(string shopperId, decimal orderTotal, CancellationToken ct)
    {
        var enrolment = await GetEnrolmentAsync(shopperId, ct);
        if (enrolment is null || !enrolment.IsAccepted)
            return 0;

        var roundUpCents = RoundUpCents(orderTotal);
        if (roundUpCents == 0)
            return 0;

        var toInvest = enrolment.AddRoundUpAndMaybeInvest(roundUpCents);
        await _enrolments.UpdateAsync(enrolment, ct);

        if (toInvest > 0)
        {
            var investment = new Investment(shopperId, toInvest);
            await _investments.AddAsync(investment, ct);
            _logger.LogInformation($"Set-aside balance reached threshold; created investment {investment.Id} for {toInvest} cents.");
        }

        return roundUpCents;
    }

    public async Task<(long PendingCents, long InvestedCents)> GetBalanceAsync(string shopperId, CancellationToken ct)
    {
        var enrolment = await GetEnrolmentAsync(shopperId, ct);
        var pending = enrolment?.PendingCents ?? 0;

        var investments = await _investments.ListAsync(new InvestmentsByShopperSpecification(shopperId), ct);
        var invested = investments.Where(i => i.Status == InvestmentStatus.Settled).Sum(i => i.AmountCents);

        return (pending, invested);
    }

    public Task<System.Collections.Generic.List<Investment>> ListInvestmentsAsync(string shopperId, CancellationToken ct) =>
        _investments.ListAsync(new InvestmentsByShopperSpecification(shopperId), ct);

    /// <summary>Cents to add to reach the next whole euro (0 when the total is already whole).</summary>
    public static long RoundUpCents(decimal orderTotal)
    {
        var totalCents = (long)Math.Round(orderTotal * 100m, MidpointRounding.AwayFromZero);
        if (totalCents < 0) totalCents = 0;
        var remainder = totalCents % 100;
        return remainder == 0 ? 0 : 100 - remainder;
    }
}
