using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class OrdersByBuyerIdSpecification : Specification<Order>
{
    public OrdersByBuyerIdSpecification(string buyerId)
    {
        Query.Where(o => o.BuyerId == buyerId)
             .Include(o => o.OrderItems)
             .OrderByDescending(o => o.OrderDate);
    }
}
