using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>All investor records with their investments, for background reconciliation.</summary>
public sealed class InvestorsWithInvestmentsSpecification : Specification<Investor>
{
    public InvestorsWithInvestmentsSpecification()
    {
        Query.Include(i => i.Investments);
    }
}
