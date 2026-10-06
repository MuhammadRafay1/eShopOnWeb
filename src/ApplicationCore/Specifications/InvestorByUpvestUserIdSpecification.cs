using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByUpvestUserIdSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByUpvestUserIdSpecification(string upvestUserId)
    {
        Query.Where(i => i.UpvestUserId == upvestUserId);
    }
}
