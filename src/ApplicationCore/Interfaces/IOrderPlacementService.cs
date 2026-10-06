using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Places an order directly from catalog items (ids and quantities), reusing the shop's existing
/// order/order-item model. Used by the public API's <c>POST /api/orders</c> endpoint, where there
/// is no basket to check out from.
/// </summary>
public interface IOrderPlacementService
{
    Task<Order> PlaceOrderAsync(string buyerId, IReadOnlyCollection<OrderLine> lines, CancellationToken cancellationToken = default);
}

public record OrderLine(int CatalogItemId, int Quantity);
