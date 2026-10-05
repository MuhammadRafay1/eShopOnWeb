using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Investments still awaiting their outcome, for the reconciliation worker.</summary>
public sealed class PendingInvestmentsSpec : Specification<Investment>
{
    public PendingInvestmentsSpec() =>
        Query.Where(i => i.Status == InvestmentStatus.Pending);
}
