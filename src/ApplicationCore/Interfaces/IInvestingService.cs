using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the invest-your-change capability: enrolment, setting aside order round-ups,
/// investing once the balance is reached, and reconciling outcomes with Upvest.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opt the shopper in. Idempotent: returns the existing enrolment if already opted in.</summary>
    Task<Investor> EnrolAsync(string shopperId, EnrolmentDetails details, CancellationToken cancellationToken);

    /// <summary>The shopper's investor record (with investments), or null if never enrolled.</summary>
    Task<Investor?> GetInvestorAsync(string shopperId, CancellationToken cancellationToken);

    /// <summary>
    /// Record a paid order for the shopper, setting aside its round-up and investing the balance
    /// if it has reached the threshold. Returns the amount set aside by this order, in cents
    /// (0 when the shopper is not an accepted investor or the order was a whole-euro amount).
    /// Never throws for investing reasons.
    /// </summary>
    Task<long> RecordPaidOrderAsync(string shopperId, decimal orderTotalEuros, CancellationToken cancellationToken);

    /// <summary>Bring every investor's enrolment and investments into line with their state at Upvest.</summary>
    Task ReconcileAllAsync(CancellationToken cancellationToken);
}
