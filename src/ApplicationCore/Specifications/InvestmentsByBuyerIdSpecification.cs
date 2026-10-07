using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A shopper's investments, newest first.</summary>
public sealed class InvestmentsByBuyerIdSpecification : Specification<Investment>
{
    public InvestmentsByBuyerIdSpecification(string buyerId)
    {
        Query.Where(i => i.BuyerId == buyerId)
            .OrderByDescending(i => i.CreatedDate);
    }
}
