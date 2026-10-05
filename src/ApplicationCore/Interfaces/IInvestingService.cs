using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the "invest your change" capability for a single shopper, identified by their
/// token identity (<paramref name="buyerId"/>). Every method scopes strictly to that shopper.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opts the shopper in, submitting them to Upvest as an investor. Idempotent per shopper.</summary>
    Task<EnrolmentResult> EnrolAsync(string buyerId, InvestorSignUp form, CancellationToken cancellationToken = default);

    /// <summary>The shopper's current enrolment, or null if they have never enrolled.</summary>
    Task<EnrolmentResult?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places and pays for an order from catalog items, reusing the app's order model, and — for an
    /// accepted investor — sets aside the round-up. Never fails because of anything to do with investing.
    /// </summary>
    Task<PlaceOrderResult> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken = default);

    /// <summary>The shopper's investments, newest first.</summary>
    Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>The shopper's balance, or null if they have never enrolled.</summary>
    Task<BalanceResult?> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies an Upvest order outcome (from a webhook) to the matching investment. Idempotent;
    /// returns true if an investment was updated. <paramref name="upvestOrderStatus"/> is the Upvest
    /// order status (e.g. "FILLED", "CANCELLED").
    /// </summary>
    Task<bool> SettleInvestmentByUpvestOrderAsync(string upvestOrderId, string upvestOrderStatus, CancellationToken cancellationToken = default);
}
