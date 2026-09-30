using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class PaymentMethodsByBuyerSpecification : Specification<PaymentMethod>
{
    public PaymentMethodsByBuyerSpecification(string buyerId)
    {
        Query.Where(m => m.BuyerId == buyerId);
    }
}
