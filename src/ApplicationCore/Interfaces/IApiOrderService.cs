using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Places an order directly from catalog item ids + quantities (no basket). Used by PublicApi,
/// which - unlike the Web storefront - has no basket to check out from. Reuses the existing
/// Order/OrderItem model; this is not a parallel order representation.
/// </summary>
public interface IApiOrderService
{
    Task<Order> PlaceOrderAsync(string buyerId, IReadOnlyList<OrderLineInput> lines, Address shipToAddress, CancellationToken cancellationToken);
}
