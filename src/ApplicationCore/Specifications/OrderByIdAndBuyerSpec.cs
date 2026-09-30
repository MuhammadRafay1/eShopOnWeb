using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// A single order scoped to its owning buyer - returns nothing for another shopper's order id.
/// </summary>
public class OrderByIdAndBuyerSpec : Specification<Order>
{
    public OrderByIdAndBuyerSpec(int orderId, string buyerId)
    {
        Query
            .Where(order => order.Id == orderId && order.BuyerId == buyerId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ItemOrdered)
            .Include(o => o.Payment!)
                .ThenInclude(p => p.Refunds);
    }
}
