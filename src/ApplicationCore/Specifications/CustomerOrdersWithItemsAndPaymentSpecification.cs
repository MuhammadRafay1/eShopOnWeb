using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// A caller's orders with their items and payment summary (status, captured / refunded amounts),
/// eagerly loaded to avoid an N+1 payment lookup per order.
/// </summary>
public class CustomerOrdersWithItemsAndPaymentSpecification : Specification<Order>
{
    public CustomerOrdersWithItemsAndPaymentSpecification(string buyerId)
    {
        Query.Where(o => o.BuyerId == buyerId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ItemOrdered);
        Query.Include(o => o.Payment)
            .ThenInclude(p => p!.Refunds);
    }
}
