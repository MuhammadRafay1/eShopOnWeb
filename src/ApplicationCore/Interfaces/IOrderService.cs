using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IOrderService
{
    Task CreateOrderAsync(int basketId, Address shippingAddress);

    /// <summary>
    /// Creates an order directly from catalog item ids + quantities (no basket), for the API-only
    /// payment flow. Reuses the same catalog-snapshot pattern as the basket path. Returns the
    /// created order (with its generated id and default AwaitingPayment status).
    /// </summary>
    Task<Order> CreateOrderAsync(string buyerId, IReadOnlyCollection<OrderItemRequest> items,
        Address shippingAddress);
}

/// <summary>A requested catalog line item for the API order-creation path.</summary>
public record OrderItemRequest(int CatalogItemId, int Quantity);
