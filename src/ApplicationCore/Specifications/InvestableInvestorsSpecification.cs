using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Accepted investors whose set-aside balance has reached the investment threshold.</summary>
public sealed class InvestableInvestorsSpecification : Specification<Investor>
{
    public InvestableInvestorsSpecification(decimal threshold)
    {
        Query.Where(i => i.Status == EnrolmentStatus.Active
            && i.UpvestAccountId != null
            && i.PendingAmount >= threshold);
    }
}
