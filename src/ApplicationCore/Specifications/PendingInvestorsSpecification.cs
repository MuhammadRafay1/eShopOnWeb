using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Investors whose enrolment is still awaiting Upvest's decision.</summary>
public sealed class PendingInvestorsSpecification : Specification<Investor>
{
    public PendingInvestorsSpecification()
    {
        Query.Where(i => i.Status == EnrolmentStatus.Pending && i.UpvestUserId != null);
    }
}
