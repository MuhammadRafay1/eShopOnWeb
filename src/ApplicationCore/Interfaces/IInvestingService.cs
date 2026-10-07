using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Drives the "invest your change" flows for a single shopper, identified by <c>shopperId</c>.
/// One shopper can never see or affect another's enrolment, ledger or investments.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opts the shopper in, creating their Upvest investor. Idempotent per shopper.</summary>
    Task<EnrolmentView> EnrolAsync(string shopperId, InvestorSignup signup, CancellationToken cancellationToken = default);

    /// <summary>The shopper's enrolment, reconciled against Upvest, or null if they have not opted in.</summary>
    Task<EnrolmentView?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken = default);

    /// <summary>The shopper's investments, newest first, each reconciled against Upvest.</summary>
    Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string shopperId, CancellationToken cancellationToken = default);

    /// <summary>The shopper's set-aside and invested balances.</summary>
    Task<BalanceView> GetBalanceAsync(string shopperId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets aside the round-up from a paid order and, once the balance reaches the threshold, invests
    /// it. Returns the amount set aside (zero when the shopper is not an accepted investor or there is
    /// nothing to round up). Never throws for any investing reason — placing an order must always succeed.
    /// </summary>
    Task<decimal> ApplyPaidOrderAsync(string shopperId, decimal orderTotal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a webhook delivered by Upvest, reconciling the affected investor in real time. Best
    /// effort and never throws — reconciliation on read is the authoritative path.
    /// </summary>
    Task HandleUpvestEventAsync(IEnumerable<string> upvestUserIds, CancellationToken cancellationToken = default);
}
