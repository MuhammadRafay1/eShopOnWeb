using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Investments whose outcome at Upvest is not yet known.</summary>
public sealed class PendingInvestmentsSpecification : Specification<Investment>
{
    public PendingInvestmentsSpecification()
    {
        Query.Where(i => i.Status == InvestmentStatus.Pending);
    }

    public PendingInvestmentsSpecification(int investorId)
    {
        Query.Where(i => i.Status == InvestmentStatus.Pending && i.InvestorId == investorId);
    }
}
