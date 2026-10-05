using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>All enrolments in a given status (used by the background processor).</summary>
public sealed class EnrolmentsByStatusSpecification : Specification<Enrolment>
{
    public EnrolmentsByStatusSpecification(EnrolmentStatus status)
    {
        Query.Where(e => e.Status == status).OrderBy(e => e.Id);
    }
}
