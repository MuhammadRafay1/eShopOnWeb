using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// A single saved card scoped to its owner — used by pay-with-saved-card, list, and delete so one
/// shopper can never see, use, or delete another's. Absence of a row drives a 404.
/// </summary>
public class PaymentMethodByIdAndBuyerIdSpec : Specification<PaymentMethod>
{
    public PaymentMethodByIdAndBuyerIdSpec(int id, string buyerId)
    {
        Query.Where(pm => pm.Id == id && pm.BuyerId == buyerId);
    }
}
