using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// One saved card, scoped to its owner. The ownership check behind paying-with and deleting a saved
/// card — a shopper can never resolve, use, or delete another's card.
/// </summary>
public sealed class PaymentMethodByIdForBuyerSpecification : Specification<PaymentMethod>, ISingleResultSpecification<PaymentMethod>
{
    public PaymentMethodByIdForBuyerSpecification(int paymentMethodId, string buyerId)
    {
        Query.Where(pm => pm.Id == paymentMethodId && pm.BuyerId == buyerId);
    }
}
