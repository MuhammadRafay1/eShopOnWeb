using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorsByStatusSpecification : Specification<Investor>
{
    public InvestorsByStatusSpecification(InvestorStatus status)
    {
        Query.Where(i => i.Status == status);
    }
}
