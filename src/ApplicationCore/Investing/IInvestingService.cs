using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// Application service coordinating the "invest your change" capability for a single shopper: opting in,
/// setting aside the round-up of paid orders, investing once the threshold is reached, and reading the
/// shopper's ledger. A shopper's data is only ever reachable through their own <c>shopperId</c>.
/// </summary>
public interface IInvestingService
{
    /// <summary>
    /// Opts the shopper in, registering them as an investor with the provider. Idempotent: if the shopper is
    /// already enrolled their existing enrolment is returned rather than registering again.
    /// </summary>
    Task<Investor> EnrolAsync(string shopperId, InvestorEnrolmentData data, CancellationToken cancellationToken);

    /// <summary>Returns the shopper's enrolment, refreshing its status from the provider, or null if not enrolled.</summary>
    Task<Investor?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a paid order: for an accepted investor, sets aside the difference between the order total and
    /// the next whole euro and invests the balance once it reaches the threshold. Returns the amount set
    /// aside by this order (0 when nothing, including when the shopper is not an accepted investor). Never
    /// throws for an investing-related reason — placing the order must not fail because of investing.
    /// </summary>
    Task<decimal> ApplyPaidOrderAsync(string shopperId, decimal orderTotal, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the shopper's investor record with its ledger and investments, refreshing the status of any
    /// pending investment from the provider, or null if not enrolled.
    /// </summary>
    Task<Investor?> GetLedgerAsync(string shopperId, CancellationToken cancellationToken);

    /// <summary>
    /// Re-syncs every shopper whose enrolment or investments are still pending against the provider. Called
    /// when the provider notifies this application of a status change (webhook).
    /// </summary>
    Task ReconcileAllAsync(CancellationToken cancellationToken);
}
