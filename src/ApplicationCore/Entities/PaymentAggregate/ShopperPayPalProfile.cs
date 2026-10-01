using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// Maps an eShop shopper to the PayPal-generated vault customer id, so later saved-card writes
/// reuse the same PayPal customer instead of creating a new one per card.
/// </summary>
public class ShopperPayPalProfile : IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private ShopperPayPalProfile() { }

    public ShopperPayPalProfile(string buyerId, string payPalCustomerId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalCustomerId, nameof(payPalCustomerId));

        BuyerId = buyerId;
        PayPalCustomerId = payPalCustomerId;
    }

    /// <summary>Primary key - the eShop buyer id (the JWT ClaimTypes.Name value).</summary>
    public string BuyerId { get; private set; }

    public string PayPalCustomerId { get; private set; }
}
