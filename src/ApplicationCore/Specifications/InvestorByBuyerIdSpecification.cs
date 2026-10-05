using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class InvestorByBuyerIdSpecification : Specification<Investor>
{
    public InvestorByBuyerIdSpecification(string buyerId)
    {
        Query
            .Where(i => i.BuyerId == buyerId)
            .Include(i => i.Investments);
    }
}
