using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// All payments with their refunds eagerly loaded. Used by reconciliation, which then filters
/// captures and refunds by timestamp in-memory (this project's scale does not warrant a SQL join).
/// </summary>
public class PaymentsWithRefundsSpecification : Specification<Payment>
{
    public PaymentsWithRefundsSpecification()
    {
        Query.Include(p => p.Refunds);
    }
}
