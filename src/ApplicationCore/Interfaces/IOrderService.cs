using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IOrderService
{
    Task CreateOrderAsync(int basketId, Address shippingAddress);

    /// <summary>
    /// Places an order directly from catalog item ids and quantities (no basket), reusing the app's
    /// existing order/order-item model. Prices come from the authoritative current catalog prices.
    /// The order starts <see cref="OrderStatus.AwaitingPayment"/>.
    /// </summary>
    Task<Order> CreateOrderFromItemsAsync(string buyerId, Address shippingAddress,
        IReadOnlyList<OrderItemRequest> items);
}

/// <summary>A requested line: a catalog item id and how many units of it.</summary>
public record OrderItemRequest(int CatalogItemId, int Quantity);
