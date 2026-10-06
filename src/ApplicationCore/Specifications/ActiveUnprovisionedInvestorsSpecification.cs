using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Accepted investors whose Upvest account has not been provisioned yet. The account may become
/// available shortly after acceptance, so provisioning is retried for these.
/// </summary>
public sealed class ActiveUnprovisionedInvestorsSpecification : Specification<Investor>
{
    public ActiveUnprovisionedInvestorsSpecification()
    {
        Query.Where(i => i.Status == EnrolmentStatus.Active &&
                         (i.UpvestAccountGroupId == null || i.UpvestAccountId == null));
    }
}
