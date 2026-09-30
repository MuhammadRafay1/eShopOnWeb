using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IOrderService
{
    Task CreateOrderAsync(int basketId, Address shippingAddress);

    /// <summary>
    /// Places an order directly from catalog item ids and quantities (used by the API), looking
    /// up prices from the catalog rather than trusting the caller. The order starts awaiting payment.
    /// </summary>
    Task<Order> CreateOrderFromItemsAsync(string buyerId, Address shippingAddress,
        IEnumerable<OrderItemRequest> items);
}

/// <summary>A requested line item: which catalog item and how many.</summary>
public record OrderItemRequest(int CatalogItemId, int Quantity);
