using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

/// <summary>
/// A card a shopper has saved for reuse. The card itself lives in PayPal's Vault; this app only
/// keeps the durable vault-token id (used to pay with the card) plus display-safe descriptors so
/// the shopper can recognise which card it is. No full card number, expiry-with-CVC, or other
/// sensitive card data is ever stored here.
///
/// Keyed directly by the raw buyer-id string (the same identity string <see cref="OrderAggregate.Order"/>
/// and Basket key on), so there is no separate Buyer row.
/// </summary>
public class PaymentMethod : BaseEntity, IAggregateRoot
{
    public string BuyerId { get; private set; }
    public string PayPalVaultId { get; private set; }
    public string CardBrand { get; private set; }
    public string Last4Digits { get; private set; }
    public string Expiry { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }

    public PaymentMethod(string buyerId, string payPalVaultId, string cardBrand,
        string last4Digits, string expiry)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));

        BuyerId = buyerId;
        PayPalVaultId = payPalVaultId;
        CardBrand = cardBrand ?? string.Empty;
        Last4Digits = last4Digits ?? string.Empty;
        Expiry = expiry ?? string.Empty;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
