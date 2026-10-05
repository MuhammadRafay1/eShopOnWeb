using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A shopper's investments, newest first.</summary>
public sealed class InvestmentsByBuyerSpecification : Specification<Investment>
{
    public InvestmentsByBuyerSpecification(string buyerId)
    {
        Query.Where(i => i.BuyerId == buyerId)
             .OrderByDescending(i => i.CreatedAt);
    }
}
