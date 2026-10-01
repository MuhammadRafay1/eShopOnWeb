using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// One order, scoped to its owner — the ownership check behind every shopper-facing order endpoint.
/// Filtering by buyer id means one shopper can never load, pay, or see another's order.
/// </summary>
public sealed class OrderByIdForBuyerSpecification : Specification<Order>, ISingleResultSpecification<Order>
{
    public OrderByIdForBuyerSpecification(int orderId, string buyerId)
    {
        Query.Where(o => o.Id == orderId && o.BuyerId == buyerId)
            .Include(o => o.OrderItems)
            .Include(o => o.Payment!)
            .ThenInclude(p => p.Refunds);
    }
}
