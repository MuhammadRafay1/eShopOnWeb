using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Local payments created within a date range, with refunds — the eShop side of reconciliation.
/// </summary>
public class PaymentsInDateRangeSpec : Specification<Payment>
{
    public PaymentsInDateRangeSpec(DateTimeOffset from, DateTimeOffset to)
    {
        Query
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .Include(p => p.Refunds);
    }
}
