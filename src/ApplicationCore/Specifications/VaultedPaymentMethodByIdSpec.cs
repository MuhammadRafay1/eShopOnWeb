using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class VaultedPaymentMethodByIdSpec : Specification<VaultedPaymentMethod>, ISingleResultSpecification<VaultedPaymentMethod>
{
    public VaultedPaymentMethodByIdSpec(int id)
    {
        Query.Where(pm => pm.Id == id);
    }
}
