using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads a single order with its items and its payment (including refunds) so payment
/// operations can act on the full aggregate.
/// </summary>
public class OrderWithPaymentByIdSpecification : Specification<Order>, ISingleResultSpecification<Order>
{
    public OrderWithPaymentByIdSpecification(int orderId)
    {
        Query.Where(o => o.Id == orderId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ItemOrdered);
        Query.Include(o => o.Payment)
            .ThenInclude(p => p!.Refunds);
    }
}
