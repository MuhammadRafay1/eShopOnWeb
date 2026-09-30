using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// eShop-side payments whose capture happened within the reconciliation window. These
/// are the local records lined up against PayPal's transaction-search results.
/// </summary>
public class PaymentsCapturedBetweenSpecification : Specification<Payment>
{
    public PaymentsCapturedBetweenSpecification(DateTimeOffset from, DateTimeOffset to)
    {
        Query.Where(p => p.CapturedAt != null && p.CapturedAt >= from && p.CapturedAt <= to)
            .Include(p => p.Refunds);
    }
}
