using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Enrolments the reconciler must still advance (not yet active or rejected).</summary>
public sealed class IncompleteEnrolmentsSpecification : Specification<Enrolment>
{
    public IncompleteEnrolmentsSpecification()
    {
        Query.Where(e => e.Stage == EnrolmentStage.AwaitingUserActivation
                      || e.Stage == EnrolmentStage.AwaitingAccountActivation);
    }
}
