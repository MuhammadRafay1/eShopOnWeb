using System.Linq;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Loads the investing account that holds the investment behind a given Upvest order id.</summary>
public sealed class InvestingAccountByUpvestOrderSpec : Specification<InvestingAccount>, ISingleResultSpecification<InvestingAccount>
{
    public InvestingAccountByUpvestOrderSpec(string upvestOrderId)
    {
        Query
            .Where(a => a.Investments.Any(i => i.UpvestOrderId == upvestOrderId))
            .Include(a => a.Investments)
            .Include(a => a.SpareChangeEntries);
    }
}
