using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Places an order from catalog item ids + quantities, reusing the app's existing
/// Order/OrderItem model, and (for an enrolled shopper) sets aside the round-up.
/// </summary>
public interface IOrderPlacementService
{
    Task<OrderPlacedResult> PlaceOrderAsync(
        string buyerId,
        IReadOnlyCollection<OrderLineRequest> lines,
        CancellationToken cancellationToken);
}

/// <summary>A requested order line: a catalog item and how many of it.</summary>
public record OrderLineRequest(int CatalogItemId, int Quantity);

/// <summary>
/// The outcome of placing an order: the new order's id and the amount set aside
/// for investing (0 when nothing was set aside).
/// </summary>
public record OrderPlacedResult(int OrderId, decimal RoundUpAmount);
