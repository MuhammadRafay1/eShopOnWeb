using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// All orders that have a payment, with their refunds, for reconciliation. The date-range overlap is applied in
/// memory (payment/capture/refund timestamps), so the whole set is loaded here.
/// </summary>
public sealed class PaidOrdersSpecification : Specification<Order>
{
    public PaidOrdersSpecification()
    {
        Query.Where(o => o.Payment != null);
        Query.Include(o => o.Payment).ThenInclude(p => p!.Refunds);
    }
}
