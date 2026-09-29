using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>All of a buyer's orders with their items — for <c>GET /api/my-orders</c>.</summary>
public class OrdersByBuyerIdSpec : Specification<Order>
{
    public OrdersByBuyerIdSpec(string buyerId)
    {
        Query.Where(o => o.BuyerId == buyerId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ItemOrdered);
    }
}
