using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>An investor by owning shopper, with their investments loaded.</summary>
public sealed class InvestorByShopperIdSpec : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByShopperIdSpec(string shopperId)
    {
        Query
            .Where(i => i.ShopperId == shopperId)
            .Include(i => i.Investments);
    }
}
