using System.Linq;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Loads the investor that owns the investment carrying a given Upvest order id.</summary>
public class InvestorByUpvestOrderSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByUpvestOrderSpecification(string upvestOrderId)
    {
        Query.Where(i => i.Investments.Any(v => v.UpvestOrderId == upvestOrderId))
            .Include(i => i.Investments)
            .Include(i => i.Ledger);
    }
}
