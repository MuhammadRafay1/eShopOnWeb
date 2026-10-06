using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestmentsByBuyerIdSpecification : Specification<Investment>
{
    public InvestmentsByBuyerIdSpecification(string buyerId)
    {
        // Newest first.
        Query.Where(i => i.BuyerId == buyerId)
             .OrderByDescending(i => i.CreatedAt)
             .ThenByDescending(i => i.Id);
    }
}
