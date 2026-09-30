using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Models.Orders;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Builds and persists an Order directly from catalog item ids/quantities (no basket involved),
/// reusing the existing Order/OrderItem aggregate, and creates the AwaitingPayment Payment for it.
/// </summary>
public interface IOrderPlacementService
{
    Task<OrderPlacementResult> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderPlacementItem> items, Address? shipToAddress, CancellationToken ct = default);
}
