using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Places an order directly from catalog items (reusing the shop's existing order model) and, for an
/// enrolled shopper, sets aside the change. Placing the order never fails because of investing.
/// </summary>
public interface IOrderPlacementService
{
    Task<OrderPlacedResult> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken);
}

/// <summary>A catalog item and quantity on an order request.</summary>
public record OrderLine(int CatalogItemId, int Quantity);

/// <summary>The outcome surfaced to the API: the new order id and the amount of change set aside.</summary>
public record OrderPlacedResult(int OrderId, decimal RoundUpAmount);
