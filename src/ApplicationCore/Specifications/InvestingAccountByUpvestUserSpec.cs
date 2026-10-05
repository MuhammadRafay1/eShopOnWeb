using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Loads the investing account linked to a given Upvest user id.</summary>
public sealed class InvestingAccountByUpvestUserSpec : Specification<InvestingAccount>, ISingleResultSpecification<InvestingAccount>
{
    public InvestingAccountByUpvestUserSpec(string upvestUserId)
    {
        Query
            .Where(a => a.UpvestUserId == upvestUserId)
            .Include(a => a.Investments)
            .Include(a => a.SpareChangeEntries);
    }
}
