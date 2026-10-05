using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Enrolment state as shown to the shopper. <see cref="Status"/> is "pending", "active" or "rejected".</summary>
public record EnrolmentView(int EnrolmentId, string Status);

/// <summary>The shopper's balance: what is set aside and not yet invested, and the total invested so far (euros).</summary>
public record BalanceView(decimal PendingAmount, decimal InvestedAmount);

/// <summary>A single investment. <see cref="Status"/> is "pending", "settled" or "failed".</summary>
public record InvestmentView(int InvestmentId, decimal Amount, string Status);

/// <summary>
/// The "Invest your change" capability, scoped to a single shopper (<c>buyerId</c>). Everything a shopper
/// sees or does flows through here; one shopper never sees another's data.
/// </summary>
public interface IInvestingService
{
    /// <summary>Opt the shopper in to investing. Idempotent per shopper.</summary>
    Task<EnrolmentView> EnrolAsync(string buyerId, InvestorEnrolmentForm form, CancellationToken cancellationToken);

    /// <summary>Where the shopper's enrolment has got to (reconciled against Upvest). Null if never enrolled.</summary>
    Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>The shopper's set-aside and invested balances. Null if never enrolled.</summary>
    Task<BalanceView?> GetBalanceAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>The shopper's investments, newest first (reconciled against Upvest).</summary>
    Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>
    /// A paid order for the shopper: set aside the round-up and, if the balance has reached the threshold,
    /// start an investment. Returns the amount set aside in euro cents (0 when nothing was set aside).
    /// Never throws for any investing reason — placing the order must still succeed.
    /// </summary>
    Task<long> HandleOrderPaidAsync(string buyerId, decimal orderTotal, CancellationToken cancellationToken);
}
