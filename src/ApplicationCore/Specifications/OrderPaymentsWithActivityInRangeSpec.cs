using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderPaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>OrderPayments whose authorize or capture activity falls within [from, to] - the eShop side of a reconciliation report.</summary>
public class OrderPaymentsWithActivityInRangeSpec : Specification<OrderPayment>
{
    public OrderPaymentsWithActivityInRangeSpec(DateTimeOffset from, DateTimeOffset to)
    {
        Query.Where(p =>
            (p.CapturedAt != null && p.CapturedAt >= from && p.CapturedAt <= to) ||
            (p.CapturedAt == null && p.AuthorizedAt != null && p.AuthorizedAt >= from && p.AuthorizedAt <= to));
    }
}
