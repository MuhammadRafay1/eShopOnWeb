using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Drives the "invest your change" capability: enrolment, setting aside change on paid orders, investing it
/// once it crosses the threshold, and reporting balances and investments. Every method is scoped to a single
/// shopper (<c>buyerId</c>); one shopper never sees another's data.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opt the shopper in, registering them as an investor with Upvest.</summary>
    Task<Investor> EnrolAsync(string buyerId, InvestorEnrolmentDetails details, CancellationToken cancellationToken = default);

    /// <summary>Where the shopper's enrolment has got to, refreshed from Upvest while still pending. Null if never enrolled.</summary>
    Task<Investor?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set aside the round-up for a paid order and, if the set-aside balance has reached the threshold, invest it.
    /// Returns the amount this order set aside (0 when it set aside nothing, or the shopper is not an accepted
    /// investor). Never throws: placing an order must never fail because of anything to do with investing.
    /// </summary>
    Task<decimal> SetAsideAndMaybeInvestAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>The shopper's current set-aside and total-invested amounts.</summary>
    Task<InvestingBalance> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>The shopper's investments, newest first, with each pending one reconciled against Upvest.</summary>
    Task<IReadOnlyList<Investment>> ListInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>Reconcile a single investment order against Upvest (used by the Upvest webhook).</summary>
    Task ReconcileOrderAsync(string upvestOrderId, CancellationToken cancellationToken = default);
}

/// <summary>What a shopper has set aside but not yet invested, and the total invested so far, in euros.</summary>
public sealed record InvestingBalance(decimal PendingAmount, decimal InvestedAmount);
