using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Places an order directly from catalog item ids and quantities, reusing the app's existing basket and
/// order model. The caller's identity is the order's buyer id.
/// </summary>
public interface IOrderPlacementService
{
    Task<Order> PlaceOrderAsync(string buyerId, IReadOnlyCollection<OrderLine> lines, CancellationToken cancellationToken = default);
}

/// <summary>One line of a placed order: a catalog item and how many of it.</summary>
public sealed record OrderLine(int CatalogItemId, int Quantity);
