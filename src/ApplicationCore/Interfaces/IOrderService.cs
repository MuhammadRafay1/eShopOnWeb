using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public record OrderItemRequest(int CatalogItemId, int Quantity);

public interface IOrderService
{
    Task CreateOrderAsync(int basketId, Address shippingAddress);

    /// <summary>
    /// Places an order directly from catalog item ids/quantities (no basket required). Reuses
    /// the existing Order/OrderItem model - the new order starts AwaitingPayment.
    /// </summary>
    Task<Order> CreateOrderAsync(string buyerId, IEnumerable<OrderItemRequest> items, Address shipToAddress);
}
