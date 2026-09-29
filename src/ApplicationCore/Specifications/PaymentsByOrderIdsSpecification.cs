using System.Collections.Generic;
using System.Linq;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Loads payments (with refunds) for a set of order ids - used by my-orders projection.</summary>
public class PaymentsByOrderIdsSpecification : Specification<Payment>
{
    public PaymentsByOrderIdsSpecification(IEnumerable<int> orderIds)
    {
        var ids = orderIds.ToArray();
        Query.Where(p => ids.Contains(p.OrderId))
            .Include(p => p.Refunds);
    }
}
