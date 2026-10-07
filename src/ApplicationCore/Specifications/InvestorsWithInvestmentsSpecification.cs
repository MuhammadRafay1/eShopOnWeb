using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads every investor together with their investments. Used to find the
/// investor that owns a given provider order; the match is done in memory
/// because the owning collection is exposed read-only.
/// </summary>
public sealed class InvestorsWithInvestmentsSpecification : Specification<Investor>
{
    public InvestorsWithInvestmentsSpecification()
    {
        Query.Include(i => i.Investments);
    }
}
