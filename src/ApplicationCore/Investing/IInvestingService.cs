using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The shop-facing operations behind the "invest your change" capability. All operations are
/// scoped to a single shopper (identified by their buyer id) so that no shopper can ever see
/// another's enrolment, ledger or investments.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opt a shopper in, onboarding them as an investor with Upvest.</summary>
    Task<EnrolmentResult> EnrolAsync(string buyerId, EnrolmentDetails details, CancellationToken cancellationToken = default);

    /// <summary>Where the shopper's enrolment has got to, or null if they have not opted in.</summary>
    Task<EnrolmentResult?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set aside the round-up for a paid order and, once the threshold is reached, queue the
    /// balance for investing. Returns the amount actually set aside (0 for a shopper who is not an
    /// accepted investor, or for an order whose total is already a whole number of euros).
    /// This never throws for investing reasons &mdash; placing an order must always succeed.
    /// </summary>
    Task<SetAsideResult> ApplyPaidOrderAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken = default);

    /// <summary>What the shopper has set aside and the total invested so far.</summary>
    Task<BalanceResult> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>The shopper's investments, newest first.</summary>
    Task<IReadOnlyList<InvestmentResult>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default);
}
