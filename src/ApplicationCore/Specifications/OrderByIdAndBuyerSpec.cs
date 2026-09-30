using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads an order only if it belongs to the given buyer - cross-user access resolves to nothing,
/// so shopper-scoped endpoints can 404 without leaking whether the order exists at all.
/// </summary>
public class OrderByIdAndBuyerSpec : Specification<Order>, ISingleResultSpecification<Order>
{
    public OrderByIdAndBuyerSpec(int orderId, string buyerId)
    {
        Query
            .Where(o => o.Id == orderId && o.BuyerId == buyerId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ItemOrdered);
    }
}
