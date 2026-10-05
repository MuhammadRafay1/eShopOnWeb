using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Enrolments still being onboarded (not yet active or rejected), for the reconciliation worker.</summary>
public sealed class OnboardingEnrolmentsSpec : Specification<Enrolment>
{
    public OnboardingEnrolmentsSpec() =>
        Query.Where(e => e.Status == EnrolmentStatus.Pending);
}

/// <summary>Active enrolments with change set aside, for the reconciliation worker to invest.</summary>
public sealed class InvestableEnrolmentsSpec : Specification<Enrolment>
{
    public InvestableEnrolmentsSpec(long thresholdCents) =>
        Query.Where(e => e.Status == EnrolmentStatus.Active && e.PendingCents >= thresholdCents);
}
