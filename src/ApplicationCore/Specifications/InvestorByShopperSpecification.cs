using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Loads a single shopper's investor record together with their investments.</summary>
public sealed class InvestorByShopperSpecification : Specification<Investor>
{
    public InvestorByShopperSpecification(string shopperId)
    {
        Query.Where(i => i.ShopperId == shopperId)
            .Include(i => i.Investments);
    }
}
