using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IOrderService
{
    Task CreateOrderAsync(int basketId, Address shippingAddress);

    /// <summary>
    /// Places an order directly from catalog item ids + quantities (no basket), reusing the same
    /// Order / OrderItem / CatalogItemOrdered model. The order starts AwaitingPayment.
    /// </summary>
    Task<Order> CreateOrderFromCatalogItemsAsync(
        string buyerId,
        Address shipToAddress,
        IReadOnlyList<(int CatalogItemId, int Quantity)> items);
}
