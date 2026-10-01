using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class OrderPaymentsUpdatedBetweenSpec : Specification<OrderPayment>
{
    public OrderPaymentsUpdatedBetweenSpec(DateTimeOffset from, DateTimeOffset to)
    {
        Query.Where(p => p.UpdatedAtUtc >= from && p.UpdatedAtUtc <= to);
    }
}
