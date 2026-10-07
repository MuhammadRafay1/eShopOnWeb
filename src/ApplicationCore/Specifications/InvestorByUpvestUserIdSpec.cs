using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Loads the investor linked to a given Upvest user id, with their investments.</summary>
public sealed class InvestorByUpvestUserIdSpec : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByUpvestUserIdSpec(string upvestUserId)
    {
        Query
            .Where(investor => investor.UpvestUserId == upvestUserId)
            .Include(investor => investor.Investments);
    }
}
