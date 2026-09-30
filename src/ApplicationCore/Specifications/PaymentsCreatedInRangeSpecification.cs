using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Payments whose first PayPal activity (creation) falls within the given window - the eShop side of reconciliation.</summary>
public class PaymentsCreatedInRangeSpecification : Specification<Payment>
{
    public PaymentsCreatedInRangeSpecification(DateTimeOffset from, DateTimeOffset to)
    {
        Query
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to && p.PaypalOrderId != null)
            .Include(p => p.Refunds);
    }
}
