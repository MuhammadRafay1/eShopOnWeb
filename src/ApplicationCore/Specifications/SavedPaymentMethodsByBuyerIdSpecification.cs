using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class SavedPaymentMethodsByBuyerIdSpecification : Specification<SavedPaymentMethod>
{
    public SavedPaymentMethodsByBuyerIdSpecification(string buyerId)
    {
        Query.Where(pm => pm.BuyerId == buyerId)
             .OrderByDescending(pm => pm.CreatedAt);
    }
}
