using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Places an order directly from catalog item ids and quantities, reusing the shop's existing
/// <see cref="Order"/> / <see cref="OrderItem"/> model. Used by the public API so an order can be
/// placed for an authenticated shopper without going through the cookie-based basket flow.
/// </summary>
public interface IOrderPlacementService
{
    Task<Order> PlaceOrderAsync(string buyerId, IEnumerable<OrderLine> lines, CancellationToken cancellationToken);
}

/// <summary>One requested order line: a catalog item and how many of it.</summary>
public record OrderLine(int CatalogItemId, int Quantity);
