using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads the payment for an order together with its owning order, refunds and event log — the full
/// context needed to fulfil, cancel or refund.
/// </summary>
public class PaymentByOrderIdSpec : Specification<Payment>
{
    public PaymentByOrderIdSpec(int orderId)
    {
        Query
            .Where(p => p.OrderId == orderId)
            .Include(p => p.Order)
            .Include(p => p.Refunds)
            .Include(p => p.Events);
    }
}
