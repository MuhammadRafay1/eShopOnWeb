using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A shopper's investments, newest first.</summary>
public sealed class InvestmentsByBuyerIdSpec : Specification<Investment>
{
    public InvestmentsByBuyerIdSpec(string buyerId) =>
        Query.Where(i => i.BuyerId == buyerId)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id);
}
