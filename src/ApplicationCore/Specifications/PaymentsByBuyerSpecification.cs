using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A shopper's payments, newest first. Scoped to one buyer so a shopper never sees another's.</summary>
public class PaymentsByBuyerSpecification : Specification<Payment>
{
    public PaymentsByBuyerSpecification(string buyerId)
    {
        Query.Where(p => p.BuyerId == buyerId)
             .OrderByDescending(p => p.CreatedAt);
    }
}
