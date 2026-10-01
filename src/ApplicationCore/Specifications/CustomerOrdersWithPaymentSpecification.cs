using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>The caller's own orders, with items and full payment/refund state, newest first.</summary>
public sealed class CustomerOrdersWithPaymentSpecification : Specification<Order>
{
    public CustomerOrdersWithPaymentSpecification(string buyerId)
    {
        Query
            .Where(o => o.BuyerId == buyerId)
            .Include(o => o.OrderItems).ThenInclude(i => i.ItemOrdered);
        Query
            .Include(o => o.Payment).ThenInclude(p => p!.Refunds);
        Query.OrderByDescending(o => o.OrderDate);
    }
}
