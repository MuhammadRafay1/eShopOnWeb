using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// All investors with their investments loaded. The settlement reconciler filters in memory for
/// investments whose outcome is not yet known; the data set is small and this keeps the query
/// portable across the SQL and in-memory providers.
/// </summary>
public sealed class InvestorsWithUnsettledInvestmentsSpecification : Specification<Investor>
{
    public InvestorsWithUnsettledInvestmentsSpecification()
    {
        Query.Include(i => i.Investments);
    }
}
