using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Returns a shopper's payments, newest first. Enforces per-shopper isolation.
/// </summary>
public class PaymentsByShopperSpecification : Specification<Payment>
{
    public PaymentsByShopperSpecification(string shopperId)
    {
        Query.Where(p => p.ShopperId == shopperId)
             .OrderByDescending(p => p.CreatedAt)
             .ThenByDescending(p => p.Id);
    }
}
