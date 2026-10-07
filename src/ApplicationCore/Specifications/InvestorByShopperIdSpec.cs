using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Loads a single shopper's investor record together with their investments.</summary>
public sealed class InvestorByShopperIdSpec : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByShopperIdSpec(string shopperId)
    {
        Query
            .Where(investor => investor.ShopperId == shopperId)
            .Include(investor => investor.Investments);
    }
}
