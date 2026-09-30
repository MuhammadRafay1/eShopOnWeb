using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// All orders that have been paid at least once (i.e. carry a Payment), used by reconciliation.
/// </summary>
public class OrdersWithPaymentSpecification : Specification<Order>
{
    public OrdersWithPaymentSpecification()
    {
        Query
            .Where(order => order.Payment != null)
            .Include(o => o.Payment!)
                .ThenInclude(p => p.Refunds);
    }
}
