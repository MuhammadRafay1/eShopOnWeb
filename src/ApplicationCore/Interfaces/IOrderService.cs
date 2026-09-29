using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IOrderService
{
    Task CreateOrderAsync(int basketId, Address shippingAddress);

    /// <summary>
    /// Creates an order directly from catalog item ids/quantities (no basket), awaiting payment.
    /// Reuses the same Order/OrderItem/CatalogItemOrdered model as the storefront checkout flow.
    /// </summary>
    Task<Order> CreatePendingOrderAsync(string buyerId, Address shippingAddress,
        IReadOnlyCollection<(int CatalogItemId, int Quantity)> items);
}
