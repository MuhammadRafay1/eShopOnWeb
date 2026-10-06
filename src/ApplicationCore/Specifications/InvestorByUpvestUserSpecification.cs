using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Loads an investor by their Upvest user id, with investments included.</summary>
public sealed class InvestorByUpvestUserSpecification : Specification<Investor>
{
    public InvestorByUpvestUserSpecification(string upvestUserId)
    {
        Query.Where(i => i.UpvestUserId == upvestUserId)
            .Include(i => i.Investments);
    }
}
