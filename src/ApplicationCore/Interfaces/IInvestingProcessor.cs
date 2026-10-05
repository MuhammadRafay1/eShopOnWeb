using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Drives the asynchronous parts of the "invest your change" capability: advancing pending
/// enrolments, investing balances that have reached the threshold, and reconciling each
/// investment's status with what actually happened at Upvest. Implementations serialise their
/// work so the background sweep and inbound webhooks never corrupt a shopper's balance.
/// </summary>
public interface IInvestingProcessor
{
    /// <summary>Set aside the change from a paid order. Never throws for investing reasons.</summary>
    /// <returns>The amount set aside, in euros (0 when nothing was set aside).</returns>
    Task<decimal> SetAsideForPaidOrderAsync(string buyerId, int orderId, decimal orderTotal, CancellationToken cancellationToken = default);

    /// <summary>Check pending enrolments against Upvest and mark them active or rejected.</summary>
    Task ReconcilePendingEnrolmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Invest the balances of any accepted investors that have reached the threshold.</summary>
    Task ExecuteReadyInvestmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Check in-flight investments against Upvest and settle or fail them.</summary>
    Task ReconcilePendingInvestmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Reconcile the single enrolment identified by an Upvest user id (webhook-triggered).</summary>
    Task ReconcileEnrolmentByUpvestUserAsync(string upvestUserId, CancellationToken cancellationToken = default);

    /// <summary>Reconcile the single investment behind an Upvest order id (webhook-triggered).</summary>
    Task ReconcileInvestmentByUpvestOrderAsync(string upvestOrderId, CancellationToken cancellationToken = default);
}
