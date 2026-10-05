using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>All investments in a given status (used by the background processor).</summary>
public sealed class InvestmentsByStatusSpecification : Specification<Investment>
{
    public InvestmentsByStatusSpecification(InvestmentStatus status)
    {
        Query.Where(i => i.Status == status).OrderBy(i => i.Id);
    }
}
