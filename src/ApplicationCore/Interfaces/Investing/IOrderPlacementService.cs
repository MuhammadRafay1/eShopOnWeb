using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;

/// <summary>
/// Places an order for a shopper directly from catalog item ids and quantities, reusing the app's existing
/// order/order-item model. Exposed so the whole "invest your change" flow is drivable through PublicApi
/// alone (its in-memory store is isolated from the Web host).
/// </summary>
public interface IOrderPlacementService
{
    Task<PlacedOrderResult> PlaceOrderAsync(
        string buyerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken);
}

/// <summary>A requested order line: a catalog item and how many of it.</summary>
public sealed record OrderLine(int CatalogItemId, int Quantity);

/// <summary>The placed order's id and its total in euros.</summary>
public sealed record PlacedOrderResult(int OrderId, decimal Total);
