using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByShopperIdSpecification : Specification<Investor>
{
    public InvestorByShopperIdSpecification(string shopperId)
    {
        Query
            .Where(i => i.ShopperId == shopperId)
            .Include(i => i.Investments);
    }
}
