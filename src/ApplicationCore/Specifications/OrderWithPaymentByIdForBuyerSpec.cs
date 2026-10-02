using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads an order with its items and payment state, scoped to the buyer who placed it — a shopper
/// never sees or acts on another's order.
/// </summary>
public class OrderWithPaymentByIdForBuyerSpec : Specification<Order>
{
    public OrderWithPaymentByIdForBuyerSpec(int orderId, string buyerId)
    {
        Query
            .Where(order => order.Id == orderId && order.BuyerId == buyerId)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.ItemOrdered)
            .Include(o => o.Payment!)
                .ThenInclude(p => p.Refunds);
    }
}
