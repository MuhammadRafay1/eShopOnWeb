using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Drives the "invest your change" flows for a single shopper, identified by their buyer id. All
/// state is scoped to that shopper: one shopper never sees another's enrolment, ledger or investments.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opt the shopper in. Idempotent: a shopper already enrolled gets their existing enrolment back.</summary>
    Task<EnrolmentView> EnrolAsync(string buyerId, InvestorEnrolmentDetails details, CancellationToken cancellationToken = default);

    /// <summary>The shopper's enrolment, refreshed from Upvest while still pending. Null if not enrolled.</summary>
    Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Record that an order has been paid. For an accepted investor this sets aside the round-up to the
    /// next whole euro and invests the balance once it reaches the threshold. Returns the amount set
    /// aside (0 if nothing was). Never throws — order placement must not fail because of investing.
    /// </summary>
    Task<decimal> RecordPaidOrderAsync(string buyerId, decimal orderTotalEur, CancellationToken cancellationToken = default);

    /// <summary>The shopper's current set-aside and total-invested amounts. Null if not enrolled.</summary>
    Task<BalanceView?> GetBalanceAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>The shopper's investments, newest first, each refreshed from Upvest while still pending.</summary>
    Task<InvestmentsView> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Background reconciliation across all investors: advance pending enrolments, invest balances that
    /// have reached the threshold, and settle pending investments against their Upvest order status.
    /// </summary>
    Task ReconcileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Advance pending enrolments and settle pending investments against Upvest, without placing any new
    /// investment. Used by the webhook so provider events never drive new orders.
    /// </summary>
    Task SettlePendingAsync(CancellationToken cancellationToken = default);
}
