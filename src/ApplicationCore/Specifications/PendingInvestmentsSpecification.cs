using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Investments placed with Upvest whose outcome is not yet known.</summary>
public sealed class PendingInvestmentsSpecification : Specification<Investment>
{
    public PendingInvestmentsSpecification()
    {
        Query.Where(i => i.Status == InvestmentStatus.Pending && i.UpvestOrderId != null);
    }
}
