using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Every order that carries a payment, with its refunds — the eShop side of reconciliation, lined up
/// against PayPal's transaction records. Operator-only (no buyer filter).
/// </summary>
public sealed class OrdersWithPaymentSpecification : Specification<Order>
{
    public OrdersWithPaymentSpecification()
    {
        Query.Where(o => o.Payment != null)
            .Include(o => o.Payment!)
            .ThenInclude(p => p.Refunds);
    }
}
