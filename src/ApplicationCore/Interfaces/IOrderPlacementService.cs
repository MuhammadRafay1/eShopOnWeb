using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public record OrderItemInput(int CatalogItemId, int Quantity);

/// <summary>
/// Builds an Order directly from catalog item ids and quantities (no basket). Used by PublicApi, whose
/// isolated in-memory store has no basket to draw from. Prices always come from the catalog, never the
/// caller.
/// </summary>
public interface IOrderPlacementService
{
    Task<Order> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderItemInput> items, Address? shipToAddress, CancellationToken cancellationToken);
}
