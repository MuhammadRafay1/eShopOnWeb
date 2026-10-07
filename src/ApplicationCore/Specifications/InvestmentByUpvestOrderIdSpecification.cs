using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestmentByUpvestOrderIdSpecification : Specification<Investment>
{
    public InvestmentByUpvestOrderIdSpecification(string upvestOrderId)
    {
        Query.Where(i => i.UpvestOrderId == upvestOrderId);
    }
}
