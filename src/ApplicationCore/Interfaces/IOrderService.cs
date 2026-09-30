using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IOrderService
{
    Task CreateOrderAsync(int basketId, Address shippingAddress);

    /// <summary>
    /// Builds and persists an order directly from catalog item ids/quantities (the PublicApi path, which has
    /// no basket). Unit prices are snapshotted from the current catalog price, mirroring the basket-based
    /// path. <paramref name="currency"/> is snapshotted onto the order so a later config change never
    /// re-prices it. Returns the persisted order (its id is needed immediately for the response).
    /// </summary>
    Task<Order> CreateOrderFromItemsAsync(
        string buyerId,
        string currency,
        Address shippingAddress,
        IEnumerable<(int CatalogItemId, int Quantity)> items);
}
