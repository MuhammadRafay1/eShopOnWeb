using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class SavedCardByIdForBuyerSpec : Specification<SavedCard>
{
    public SavedCardByIdForBuyerSpec(int paymentMethodId, string buyerId)
    {
        Query.Where(c => c.Id == paymentMethodId && c.BuyerId == buyerId);
    }
}
