using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Application service for the "invest your change" feature. Keeps each shopper's enrolment, ledger and
/// investments private to that shopper, and never fails an order because of anything to do with investing.
/// </summary>
public interface IInvestingService
{
    /// <summary>
    /// Opts a shopper in: creates the Upvest investor and starts onboarding. Idempotent — a shopper who
    /// already has an enrolment gets their existing one back rather than a second investor.
    /// </summary>
    Task<EnrolmentView> EnrolAsync(string buyerId, InvestorSignupForm form, CancellationToken cancellationToken);

    /// <summary>The caller's enrolment, or null if they have not opted in.</summary>
    Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a shopper's paid order against their change ledger. Returns the amount set aside (the
    /// round-up to the next whole euro), or 0 when nothing was set aside (not enrolled/accepted, or the
    /// total was already whole). Never throws for investing reasons.
    /// </summary>
    Task<decimal> RecordPaidOrderAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken);

    /// <summary>The caller's current set-aside and invested totals, or null if not enrolled.</summary>
    Task<BalanceView?> GetBalanceAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>The caller's investments, newest first.</summary>
    Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken);
}

/// <summary>Enrolment as surfaced by the API. <paramref name="Status"/> is "pending"/"active"/"rejected".</summary>
public record EnrolmentView(int EnrolmentId, string Status);

/// <summary>Balance as surfaced by the API, in euro cents (formatted to 2dp euros at the edge).</summary>
public record BalanceView(long PendingCents, long InvestedCents);

/// <summary>An investment as surfaced by the API. <paramref name="Status"/> is "pending"/"settled"/"failed".</summary>
public record InvestmentView(int InvestmentId, long AmountCents, string Status);
