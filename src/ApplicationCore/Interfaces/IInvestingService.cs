using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The shop-facing operations of the "invest your change" capability. Every method is scoped to
/// a single shopper and only ever touches that shopper's own enrolment, ledger and investments.
/// </summary>
public interface IInvestingService
{
    /// <summary>
    /// Opts the shopper in: registers them with Upvest and records a pending enrolment. If the
    /// shopper is already enrolled, the existing enrolment is returned unchanged.
    /// </summary>
    Task<Investor> EnrolAsync(string shopperId, InvestorEnrolmentDetails details, CancellationToken cancellationToken = default);

    /// <summary>Returns the shopper's enrolment, or null if they have not opted in.</summary>
    Task<Investor?> GetEnrolmentAsync(string shopperId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets aside the round-up for a shopper's freshly paid order and, if the set-aside balance
    /// has reached the threshold, invests it. Returns the amount this order set aside (0 when
    /// none). Never throws on account of investing — callers rely on this being safe.
    /// </summary>
    Task<decimal> ProcessPaidOrderAsync(string shopperId, decimal orderTotal, CancellationToken cancellationToken = default);

    /// <summary>The shopper's investments, newest first.</summary>
    Task<IReadOnlyList<Investment>> GetInvestmentsAsync(string shopperId, CancellationToken cancellationToken = default);
}
