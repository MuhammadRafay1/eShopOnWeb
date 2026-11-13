using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A caller's payments, newest first.</summary>
public sealed class PortfolioPaymentsByBuyerSpecification : Specification<PortfolioPayment>
{
    public PortfolioPaymentsByBuyerSpecification(string buyerId)
    {
        Query
            .Where(p => p.BuyerId == buyerId)
            .OrderByDescending(p => p.CreatedAt);
    }
}
