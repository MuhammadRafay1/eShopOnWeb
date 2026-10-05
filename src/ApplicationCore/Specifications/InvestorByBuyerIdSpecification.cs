using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByBuyerIdSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByBuyerIdSpecification(string buyerId)
    {
        Query.Where(i => i.BuyerId == buyerId);
    }
}
