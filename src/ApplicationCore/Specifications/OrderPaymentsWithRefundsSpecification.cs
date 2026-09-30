using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads payments (with their refunds) whose activity could plausibly fall in a reconciliation window.
/// Filters by <see cref="OrderPayment.UpdatedAt"/> with a caller-supplied buffer, since eShop does not store
/// PayPal's own per-transaction timestamps precisely.
/// </summary>
public class OrderPaymentsWithRefundsSpecification : Specification<OrderPayment>
{
    public OrderPaymentsWithRefundsSpecification(DateTimeOffset from, DateTimeOffset to)
    {
        Query.Where(p => p.UpdatedAt >= from && p.UpdatedAt <= to)
             .Include(p => p.Refunds);
    }
}
