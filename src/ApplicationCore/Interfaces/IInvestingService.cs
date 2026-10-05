using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the "invest your change" capability over the domain and the Upvest gateway.
/// Every method scopes to a single shopper; one shopper never sees another's data.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opt the shopper in. Returns the existing enrolment if they are already enrolled.</summary>
    Task<Investor> EnrolAsync(string shopperId, EnrolmentForm form, CancellationToken cancellationToken);

    /// <summary>The shopper's enrolment, refreshing a still-pending acceptance from Upvest. Null if not enrolled.</summary>
    Task<Investor?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken);

    /// <summary>
    /// The shopper's investor with its investments, settling any still-pending tranche against Upvest.
    /// Null if the shopper is not enrolled.
    /// </summary>
    Task<Investor?> GetInvestorAsync(string shopperId, CancellationToken cancellationToken);

    /// <summary>
    /// Handle a paid order: for an accepted investor, set aside the round-up to the next whole euro and,
    /// once the balance reaches €10, invest it. Returns the amount set aside by this order, in euro-cents
    /// (0 when nothing was set aside). Never throws — investing must not affect order placement.
    /// </summary>
    Task<long> HandlePaidOrderAsync(string shopperId, decimal orderTotalInEuros, CancellationToken cancellationToken);

    /// <summary>
    /// Reconcile the investor referenced by an Upvest id (from a callback): refresh acceptance and settle
    /// any pending investment against the authoritative Upvest API. A no-op if nothing matches.
    /// </summary>
    Task ReconcileAsync(System.Guid upvestId, CancellationToken cancellationToken);
}
