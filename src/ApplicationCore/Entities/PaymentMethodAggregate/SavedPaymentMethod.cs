using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

/// <summary>
/// A card a shopper has saved with PayPal's vault for reuse on a later order. Only safe, non-sensitive
/// card data is stored here; the PAN/CVC are never persisted by this application - see
/// <see cref="PayPalVaultId"/>, which is the only thing needed to pay with this card again.
/// </summary>
public class SavedPaymentMethod : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private SavedPaymentMethod() { }
#pragma warning restore CS8618

    public SavedPaymentMethod(string buyerId, string payPalVaultId, string payPalCustomerId, string brand, string last4, string expiry, string? label)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));
        Guard.Against.NullOrEmpty(payPalCustomerId, nameof(payPalCustomerId));

        BuyerId = buyerId;
        PayPalVaultId = payPalVaultId;
        PayPalCustomerId = payPalCustomerId;
        Brand = brand;
        Last4 = last4;
        Expiry = expiry;
        Label = label;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string BuyerId { get; private set; }
    public string PayPalVaultId { get; private set; }
    public string PayPalCustomerId { get; private set; }
    public string Brand { get; private set; }
    public string Last4 { get; private set; }
    public string Expiry { get; private set; }
    public string? Label { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
