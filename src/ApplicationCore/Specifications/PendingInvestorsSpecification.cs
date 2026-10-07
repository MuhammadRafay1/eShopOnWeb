using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Enrolments still awaiting Upvest's acceptance that have an account to check.</summary>
public sealed class PendingInvestorsSpecification : Specification<Investor>
{
    public PendingInvestorsSpecification()
    {
        Query.Where(i => i.Status == EnrolmentStatus.Pending && i.UpvestAccountId != null);
    }
}
