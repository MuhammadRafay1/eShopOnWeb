using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Payments (with refunds) for a set of order ids — used to build the reconciliation report.</summary>
public class PaymentsByOrderIdsSpec : Specification<Payment>
{
    public PaymentsByOrderIdsSpec(IEnumerable<int> orderIds)
    {
        var ids = orderIds.ToArray();
        Query.Where(p => ids.Contains(p.OrderId))
            .Include(p => p.Refunds);
    }
}
