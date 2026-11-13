using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Finds the investor record belonging to a single shopper. Enforces per-shopper isolation:
/// a shopper only ever resolves their own investor record.
/// </summary>
public class InvestorByShopperSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByShopperSpecification(string shopperId)
    {
        Query.Where(i => i.ShopperId == shopperId);
    }
}
