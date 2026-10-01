using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class VaultedPaymentMethodsByBuyerSpec : Specification<VaultedPaymentMethod>
{
    public VaultedPaymentMethodsByBuyerSpec(string buyerId)
    {
        Query.Where(pm => pm.BuyerId == buyerId)
            .OrderByDescending(pm => pm.CreatedDate);
    }
}
