using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A buyer's saved cards — for <c>GET /api/payment-methods</c>.</summary>
public class PaymentMethodsByBuyerIdSpec : Specification<PaymentMethod>
{
    public PaymentMethodsByBuyerIdSpec(string buyerId)
    {
        Query.Where(pm => pm.BuyerId == buyerId);
    }
}
