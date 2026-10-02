using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Every order eShop has captured a payment for, so a reconciliation report can line up PayPal's own
/// record of transactions against what eShop knows about.
/// </summary>
public class OrdersWithCapturedPaymentSpecification : Specification<Order>
{
    public OrdersWithCapturedPaymentSpecification()
    {
        Query
            .Include(o => o.Payment!)
            .Where(o => o.Payment != null && o.Payment.CaptureId != null);
    }
}
