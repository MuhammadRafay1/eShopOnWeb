using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class SavedCardByIdAndBuyerSpecification : Specification<SavedPaymentMethod>
{
    public SavedCardByIdAndBuyerSpecification(int paymentMethodId, string buyerId)
    {
        Query.Where(c => c.Id == paymentMethodId && c.BuyerId == buyerId);
    }
}
