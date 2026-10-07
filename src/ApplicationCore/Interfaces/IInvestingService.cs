using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Drives the "invest your change" capability: enrolment, setting aside change from paid orders,
/// investing once enough has accrued, and reconciling statuses with Upvest. Every method is scoped to
/// a single shopper (<paramref name="buyerId"/>); a shopper only ever sees their own data.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opt the shopper in, submitting their sign-up form to Upvest. Idempotent per shopper.</summary>
    Task<Investor> EnrolAsync(string buyerId, InvestorSignUpForm form, CancellationToken cancellationToken);

    /// <summary>The shopper's enrolment, or null if they have not opted in.</summary>
    Task<Investor?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>
    /// Record that an order has been paid: for an accepted investor, set aside the round-up to the next
    /// whole euro and return the amount set aside. Returns 0 for anyone who is not an accepted investor
    /// or whose order total was already a whole number of euros. Never throws.
    /// </summary>
    Task<decimal> SetAsideFromPaidOrderAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken);

    /// <summary>The shopper's set-aside and invested balances.</summary>
    Task<InvestorBalance> GetBalanceAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>The shopper's investments, newest first.</summary>
    Task<System.Collections.Generic.IReadOnlyList<Investment>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>
    /// One reconciliation pass over every shopper: advance pending enrolments to accepted/rejected,
    /// invest any set-aside balance that has reached the threshold, and settle pending investments —
    /// all from the authoritative state at Upvest. Safe to call on a timer and on a webhook.
    /// </summary>
    Task ReconcileAsync(CancellationToken cancellationToken);
}

/// <summary>What a shopper has set aside and what they have invested so far, in euros.</summary>
public record InvestorBalance(decimal PendingAmount, decimal InvestedAmount);
