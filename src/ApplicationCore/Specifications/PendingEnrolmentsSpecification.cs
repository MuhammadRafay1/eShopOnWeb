using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Enrolments still awaiting Upvest's acceptance decision.</summary>
public sealed class PendingEnrolmentsSpecification : Specification<Investor>
{
    public PendingEnrolmentsSpecification()
    {
        Query.Where(i => i.Status == EnrolmentStatus.Pending);
    }
}
