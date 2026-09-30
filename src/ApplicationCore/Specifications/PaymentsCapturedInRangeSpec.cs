using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Local payments that were captured (and therefore should have produced a PayPal transaction)
/// within the given window - the eShop side of a reconciliation report.
/// </summary>
public class PaymentsCapturedInRangeSpec : Specification<Payment>
{
    public PaymentsCapturedInRangeSpec(DateTimeOffset from, DateTimeOffset to)
    {
        Query
            .Where(p => p.CapturedAt != null && p.CapturedAt >= from && p.CapturedAt <= to)
            .Include(p => p.Refunds);
    }
}
