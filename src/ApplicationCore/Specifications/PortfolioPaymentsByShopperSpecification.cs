using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// A single shopper's pay-ins, newest first. The shopper filter enforces that one shopper never sees
/// another's payments.
/// </summary>
public sealed class PortfolioPaymentsByShopperSpecification : Specification<PortfolioPayment>
{
    public PortfolioPaymentsByShopperSpecification(string shopperKey)
    {
        Query.Where(p => p.ShopperKey == shopperKey)
             .OrderByDescending(p => p.CreatedAt);
    }
}
