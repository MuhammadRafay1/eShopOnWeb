using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the "invest your change" capability: enrolling shoppers as Upvest investors,
/// setting aside the round-up from paid orders, investing once the balance is high enough,
/// and keeping each investment's status in step with what happened at Upvest.
/// </summary>
public interface IInvestingService
{
    /// <summary>
    /// Opts the shopper in. Creates the Upvest user and starts onboarding. The returned investor
    /// starts <see cref="EnrolmentStatus.Pending"/> until Upvest accepts them.
    /// </summary>
    Task<Investor> EnrolAsync(string buyerId, InvestorSignup signup, CancellationToken cancellationToken);

    /// <summary>Returns the shopper's enrolment, or null if they have never opted in.</summary>
    Task<Investor?> GetInvestorAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>
    /// Records that the shopper has paid for an order totalling <paramref name="orderTotal"/> euros.
    /// For an accepted investor this sets aside the difference up to the next whole euro and returns
    /// that amount; for anyone else it sets nothing aside and returns 0. Never throws.
    /// </summary>
    Task<decimal> RecordPaidOrderAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken);

    /// <summary>The shopper's investments, newest first.</summary>
    Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>What the shopper currently has set aside and the total invested so far.</summary>
    Task<BalanceSummary> GetBalanceAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>
    /// Advances all outstanding work by reconciling against Upvest: activating pending enrolments,
    /// provisioning accounts, investing balances that have reached the threshold, and settling
    /// placed investments. Safe to call repeatedly; used by the background reconciler.
    /// </summary>
    Task ProcessDueWorkAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Processes a single webhook event delivered by Upvest, reconciling the referenced resource.
    /// Idempotent — safe to receive duplicate deliveries.
    /// </summary>
    Task HandleWebhookEventAsync(string eventType, string? resourceId, CancellationToken cancellationToken);
}
