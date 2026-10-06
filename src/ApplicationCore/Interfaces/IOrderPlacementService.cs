using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>A single line of a placed order: how many of which catalog item.</summary>
public record OrderLine(int CatalogItemId, int Quantity);

/// <summary>The outcome of placing an order.</summary>
public record OrderPlacementResult(int OrderId, decimal Total);

/// <summary>
/// Places an order straight from catalog item ids and quantities, reusing the application's
/// existing order/order-item model. Used by the PublicApi order endpoint.
/// </summary>
public interface IOrderPlacementService
{
    Task<OrderPlacementResult> PlaceOrderAsync(string buyerId, IReadOnlyCollection<OrderLine> lines, CancellationToken cancellationToken);
}
