using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// All payments that have been captured at least once - the eShop side of the reconciliation report.
/// </summary>
public class CapturedPaymentsSpec : Specification<Payment>
{
    public CapturedPaymentsSpec()
    {
        Query
            .Where(p => p.CaptureId != null)
            .Include(p => p.Refunds);
    }
}
