using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A shopper's investments, newest first.</summary>
public sealed class InvestmentsByShopperSpecification : Specification<Investment>
{
    public InvestmentsByShopperSpecification(string shopperId)
    {
        Query.Where(i => i.ShopperId == shopperId)
             .OrderByDescending(i => i.CreatedAt);
    }
}
