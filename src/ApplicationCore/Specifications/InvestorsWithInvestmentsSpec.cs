using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>All investors, with their investments loaded. Used by the background reconciler.</summary>
public sealed class InvestorsWithInvestmentsSpec : Specification<Investor>
{
    public InvestorsWithInvestmentsSpec()
    {
        Query.Include(i => i.Investments);
    }
}
