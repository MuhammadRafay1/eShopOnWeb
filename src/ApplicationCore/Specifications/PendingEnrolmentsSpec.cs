using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads enrolments that have been registered with Upvest but not yet accepted, so their
/// status can be reconciled against Upvest.
/// </summary>
public sealed class PendingEnrolmentsSpec : Specification<InvestingAccount>
{
    public PendingEnrolmentsSpec()
    {
        Query
            .Where(a => a.Status == EnrolmentStatus.Pending && a.UpvestUserId != null)
            .Include(a => a.Investments)
            .Include(a => a.SpareChangeEntries);
    }
}
