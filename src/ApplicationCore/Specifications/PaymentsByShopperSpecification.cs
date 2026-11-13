using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class PaymentsByShopperSpecification : Specification<InvestorPayment>
{
    /// <summary>The caller's payments, newest first.</summary>
    public PaymentsByShopperSpecification(string shopperId)
    {
        Query.Where(p => p.ShopperId == shopperId)
             .OrderByDescending(p => p.CreatedAt)
             .ThenByDescending(p => p.Id);
    }
}
