using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// A single order scoped to its owner — used by shopper-facing actions (pay) so one shopper can
/// never act on another's order. Returning no row (rather than an unfiltered one) is what lets
/// the endpoint answer 404 for a resource the caller doesn't own.
/// </summary>
public class OrderByIdAndBuyerIdSpec : Specification<Order>
{
    public OrderByIdAndBuyerIdSpec(int orderId, string buyerId)
    {
        Query.Where(o => o.Id == orderId && o.BuyerId == buyerId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ItemOrdered);
    }
}
