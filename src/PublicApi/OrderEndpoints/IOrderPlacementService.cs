using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>A requested line on a new order.</summary>
public record OrderLine(int CatalogItemId, int Quantity);

/// <summary>Outcome of placing an order, including how much change it set aside.</summary>
public record OrderPlacementResult(bool Success, int OrderId, decimal RoundUpAmount, string? Error);

/// <summary>
/// Places an order from catalog items using the app's existing order model, treats it as paid,
/// and sets aside the round-up for enrolled investors. Placing an order never fails for any
/// investing reason.
/// </summary>
public interface IOrderPlacementService
{
    Task<OrderPlacementResult> PlaceAsync(string buyerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken = default);
}
