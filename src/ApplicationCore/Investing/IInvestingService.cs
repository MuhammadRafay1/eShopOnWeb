using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// Orchestrates the "invest your change" capability: enrolment, setting aside spare change from
/// paid orders, investing it once it reaches the threshold, and reporting balances and investments.
/// Every method scopes its data to a single shopper.
/// </summary>
public interface IInvestingService
{
    /// <summary>
    /// Starts (or returns the existing) enrolment for a shopper, registering them with Upvest.
    /// </summary>
    Task<InvestorEnrolment> EnrolAsync(string shopperId, UpvestInvestorRegistration registration, CancellationToken cancellationToken);

    Task<InvestorEnrolment?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken);

    /// <summary>
    /// Records that a shopper's order has been paid and sets aside its round-up if the shopper is an
    /// accepted investor. Returns the amount set aside (0 otherwise). Never throws: investing must
    /// never cause order placement to fail.
    /// </summary>
    Task<decimal> HandlePaidOrderAsync(string shopperId, decimal orderTotal, CancellationToken cancellationToken);

    Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string shopperId, CancellationToken cancellationToken);

    Task<InvestingBalance> GetBalanceAsync(string shopperId, CancellationToken cancellationToken);

    /// <summary>Advances pending enrolments towards acceptance, reflecting the state held at Upvest.</summary>
    Task ProgressEnrolmentsAsync(CancellationToken cancellationToken);

    /// <summary>Executes due investments at Upvest and reconciles in-flight ones to their outcome.</summary>
    Task ProcessInvestmentsAsync(CancellationToken cancellationToken);

    /// <summary>Applies a settlement outcome learned from an Upvest order/execution webhook.</summary>
    Task ApplyOrderOutcomeAsync(string upvestOrderId, string upvestStatus, CancellationToken cancellationToken);
}

/// <summary>A shopper's current set-aside and total invested amounts, in euros.</summary>
public sealed record InvestingBalance(decimal PendingAmount, decimal InvestedAmount);
