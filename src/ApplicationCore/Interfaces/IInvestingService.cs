using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Sets aside the spare change from a paid order and invests it once a
/// shopper's balance reaches the threshold.
/// </summary>
public interface IInvestingService
{
    /// <summary>
    /// Called when an order has been paid. For an accepted investor, sets aside
    /// the round-up and invests the balance if it has reached the threshold.
    /// Never throws — investing must never break order placement.
    /// Returns the amount this order set aside (0 if nothing was set aside).
    /// </summary>
    Task<decimal> HandleOrderPaidAsync(string buyerId, int orderId, decimal orderTotal, CancellationToken ct);

    /// <summary>
    /// Invests the whole set-aside balance of an already-loaded investor if it
    /// has reached the threshold. Used by reconciliation to retry/catch up.
    /// </summary>
    Task TryInvestAsync(Investor investor, CancellationToken ct);
}
