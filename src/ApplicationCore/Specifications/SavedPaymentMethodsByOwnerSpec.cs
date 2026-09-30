using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class SavedPaymentMethodsByOwnerSpec : Specification<SavedPaymentMethod>
{
    public SavedPaymentMethodsByOwnerSpec(string ownerId)
    {
        Query.Where(m => m.OwnerId == ownerId).OrderByDescending(m => m.CreatedAt);
    }
}
