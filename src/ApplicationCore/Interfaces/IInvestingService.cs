using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The shopper-facing investing capability. All reads and writes are scoped to a
/// single shopper (<c>buyerId</c>); one shopper never sees another's data.
/// </summary>
public interface IInvestingService
{
    /// <summary>
    /// Opt the shopper in. Idempotent: if the shopper is already enrolled the
    /// existing enrolment is returned without creating a second investor.
    /// </summary>
    Task<EnrolmentView> EnrolAsync(string buyerId, InvestorDetails details, CancellationToken cancellationToken);

    /// <summary>The shopper's enrolment, or null if they have never enrolled.</summary>
    Task<EnrolmentView?> GetEnrolmentAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>
    /// Set aside the round-up for a paid order and return the amount set aside
    /// (0 when the shopper is not an accepted investor, or the total was already a
    /// whole number of euros). Never throws — a failure here must not fail the order.
    /// </summary>
    Task<decimal> SetAsideForPaidOrderAsync(string buyerId, int orderId, decimal orderTotal, CancellationToken cancellationToken);

    /// <summary>What the shopper has set aside but not yet invested, and the total invested so far.</summary>
    Task<BalanceView> GetBalanceAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>The shopper's investments, newest first.</summary>
    Task<IReadOnlyList<InvestmentView>> GetInvestmentsAsync(string buyerId, CancellationToken cancellationToken);
}
