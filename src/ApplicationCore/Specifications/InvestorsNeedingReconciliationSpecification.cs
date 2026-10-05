using System.Linq;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Investors with onboarding still in progress, or with investments still awaiting their
/// outcome at Upvest. Used by the background reconciler.
/// </summary>
public class InvestorsNeedingReconciliationSpecification : Specification<Investor>
{
    public InvestorsNeedingReconciliationSpecification()
    {
        Query
            .Where(i => i.Status == EnrolmentStatus.Pending
                        || i.Investments.Any(inv => inv.Status == InvestmentStatus.Pending))
            .Include(i => i.Investments);
    }
}
