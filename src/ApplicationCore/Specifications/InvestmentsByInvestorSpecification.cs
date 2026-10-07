using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A shopper's investments, newest first.</summary>
public sealed class InvestmentsByInvestorSpecification : Specification<Investment>
{
    public InvestmentsByInvestorSpecification(int investorId)
    {
        Query
            .Where(i => i.InvestorId == investorId)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id);
    }
}
