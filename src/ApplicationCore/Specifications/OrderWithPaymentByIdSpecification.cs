using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// One order by id, with its payment and refunds, and no buyer filter. Used only by operator
/// endpoints (fulfil/cancel/refund), which are already restricted to the administrator role.
/// </summary>
public sealed class OrderWithPaymentByIdSpecification : Specification<Order>, ISingleResultSpecification<Order>
{
    public OrderWithPaymentByIdSpecification(int orderId)
    {
        Query.Where(o => o.Id == orderId)
            .Include(o => o.OrderItems)
            .Include(o => o.Payment!)
            .ThenInclude(p => p.Refunds);
    }
}
