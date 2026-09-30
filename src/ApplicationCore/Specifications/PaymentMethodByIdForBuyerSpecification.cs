using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// A single saved card, scoped to the owning shopper. Used both to look up a card for display /
/// deletion and to authorize that a paymentMethodId named in a pay request belongs to the caller
/// before it is ever sent to PayPal. A card that exists but belongs to someone else yields no
/// result — the endpoint then 404s exactly like a foreign order id would.
/// </summary>
public class PaymentMethodByIdForBuyerSpecification : Specification<PaymentMethod>, ISingleResultSpecification<PaymentMethod>
{
    public PaymentMethodByIdForBuyerSpecification(string payPalVaultId, string buyerId)
    {
        Query.Where(p => p.PayPalVaultId == payPalVaultId && p.BuyerId == buyerId);
    }
}
