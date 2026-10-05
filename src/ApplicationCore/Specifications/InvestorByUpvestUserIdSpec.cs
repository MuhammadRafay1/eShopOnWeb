using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>An investor by their Upvest user id, with their investments loaded.</summary>
public sealed class InvestorByUpvestUserIdSpec : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByUpvestUserIdSpec(string upvestUserId)
    {
        Query
            .Where(i => i.UpvestUserId == upvestUserId)
            .Include(i => i.Investments);
    }
}
