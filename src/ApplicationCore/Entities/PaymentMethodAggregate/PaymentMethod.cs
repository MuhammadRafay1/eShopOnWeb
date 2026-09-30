using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

/// <summary>
/// A card a shopper has saved (vaulted at PayPal) for reuse. Keyed by PayPal's own vault
/// payment-token id — that id <em>is</em> eShop's paymentMethodId, so there is no separate local
/// key to map. Holds only display-safe descriptors; the PAN and CVV are never stored here.
/// </summary>
public class PaymentMethod : IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }

    public PaymentMethod(string payPalVaultId, string buyerId, string payPalCustomerId,
        string brand, string last4, int expiryMonth, int expiryYear)
    {
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalCustomerId, nameof(payPalCustomerId));

        PayPalVaultId = payPalVaultId;
        BuyerId = buyerId;
        PayPalCustomerId = payPalCustomerId;
        Brand = brand;
        Last4 = last4;
        ExpiryMonth = expiryMonth;
        ExpiryYear = expiryYear;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>PayPal vault payment-token id; the primary key and the shopper-facing paymentMethodId.</summary>
    public string PayPalVaultId { get; private set; }

    /// <summary>Owning shopper (the JWT identity string, same convention as Order.BuyerId).</summary>
    public string BuyerId { get; private set; }

    /// <summary>PayPal-generated customer id, reused across a buyer's saved cards for listing/lookup.</summary>
    public string PayPalCustomerId { get; private set; }

    public string Brand { get; private set; }
    public string Last4 { get; private set; }
    public int ExpiryMonth { get; private set; }
    public int ExpiryYear { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Human-recognisable, PAN-free label, e.g. "VISA ending in 1111, exp 12/2028".</summary>
    public string DisplayName => $"{Brand} ending in {Last4}, exp {ExpiryMonth:00}/{ExpiryYear}";
}
