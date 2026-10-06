using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Accepted investors, with their investments loaded, for the investing reconciler.</summary>
public sealed class ActiveInvestorsSpecification : Specification<Investor>
{
    public ActiveInvestorsSpecification()
    {
        Query.Where(i => i.Status == EnrolmentStatus.Active)
            .Include(i => i.Investments);
    }
}
