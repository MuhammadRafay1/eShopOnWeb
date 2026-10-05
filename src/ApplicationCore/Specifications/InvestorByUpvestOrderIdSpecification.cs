using System.Linq;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class InvestorByUpvestOrderIdSpecification : Specification<Investor>
{
    public InvestorByUpvestOrderIdSpecification(string upvestOrderId)
    {
        Query
            .Where(i => i.Investments.Any(inv => inv.UpvestOrderId == upvestOrderId))
            .Include(i => i.Investments);
    }
}
