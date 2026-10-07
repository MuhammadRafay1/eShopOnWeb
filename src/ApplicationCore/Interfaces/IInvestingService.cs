using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the "invest your change" feature for a single shopper: enrolment, setting aside the
/// round-up from paid orders, investing the accrued balance, and reconciling outcomes with Upvest.
/// Every operation is scoped to the shopper's own <c>buyerId</c>.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opts the shopper in, registering them as an investor at Upvest. Idempotent per shopper.</summary>
    Task<Investor> EnrolAsync(string buyerId, InvestorEnrolmentDetails details, CancellationToken cancellationToken = default);

    /// <summary>Returns the shopper's enrolment, reconciling its acceptance state with Upvest first.</summary>
    Task<Investor?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets aside the round-up from one paid order and, once the balance reaches the threshold,
    /// invests it. Never throws: a failure here must not fail the order. Returns the amount set aside.
    /// </summary>
    Task<decimal> RecordPaidOrderAsync(string buyerId, int orderId, decimal orderTotal, CancellationToken cancellationToken = default);

    /// <summary>Returns the shopper's investments (newest first), reconciling their outcomes with Upvest first.</summary>
    Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>Returns the shopper's not-yet-invested balance and total invested, reconciled with Upvest.</summary>
    Task<InvestingBalance> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles the investment carrying a given Upvest order id with its current outcome at Upvest.
    /// Used by the Upvest webhook callback. No shopper token is involved; the Upvest order id selects
    /// the owning investor. Never throws.
    /// </summary>
    Task ReconcileByUpvestOrderAsync(string upvestOrderId, CancellationToken cancellationToken = default);
}

/// <summary>A shopper's spare-change balance: what is set aside and what has been invested, in euros.</summary>
public record InvestingBalance(decimal PendingAmount, decimal InvestedAmount);
