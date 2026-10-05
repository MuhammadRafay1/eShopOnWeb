using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestmentsByStatusSpecification : Specification<Investment>
{
    public InvestmentsByStatusSpecification(InvestmentStatus status)
    {
        Query.Where(i => i.Status == status)
             .OrderBy(i => i.CreatedDate);
    }
}
