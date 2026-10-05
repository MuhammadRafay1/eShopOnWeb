using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestmentsByInvestorSpecification : Specification<Investment>
{
    /// <summary>Investments for one investor, newest first.</summary>
    public InvestmentsByInvestorSpecification(int investorId)
    {
        Query.Where(i => i.InvestorId == investorId)
             .OrderByDescending(i => i.CreatedAt);
    }
}

public sealed class PendingInvestmentsSpecification : Specification<Investment>
{
    /// <summary>Investments whose outcome is not yet known.</summary>
    public PendingInvestmentsSpecification(int investorId)
    {
        Query.Where(i => i.InvestorId == investorId && i.Status == InvestmentStatus.Pending);
    }
}
