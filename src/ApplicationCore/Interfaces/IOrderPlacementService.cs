using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Places an order from catalog items (reusing the existing order model) and, for an enrolled
/// shopper, sets aside the order's rounding difference towards investing.
/// </summary>
public interface IOrderPlacementService
{
    Task<OrderPlacementResult> PlaceAsync(string buyerId, IReadOnlyCollection<OrderLine> lines, CancellationToken cancellationToken = default);
}

/// <summary>One requested catalog line.</summary>
public record OrderLine(int CatalogItemId, int Quantity);

/// <summary>The id of the placed order and the amount it set aside (0 if nothing).</summary>
public record OrderPlacementResult(int OrderId, decimal RoundUpAmount);
