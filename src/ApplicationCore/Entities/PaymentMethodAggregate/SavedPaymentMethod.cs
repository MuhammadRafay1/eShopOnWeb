using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

/// <summary>
/// A card a shopper has saved (vaulted) with PayPal for reuse. Belongs to exactly one shopper
/// (<see cref="BuyerId"/> == the JWT username, the same identity convention as <c>Order.BuyerId</c>).
/// Stores only the PayPal vault reference and safe-to-display metadata — never a PAN or CVV.
/// </summary>
public class SavedPaymentMethod : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private SavedPaymentMethod() { }

    public SavedPaymentMethod(
        string buyerId,
        string payPalVaultId,
        string? cardBrand,
        string? lastFour,
        string? expiry,
        string? cardholderName)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));

        BuyerId = buyerId;
        PayPalVaultId = payPalVaultId;
        CardBrand = cardBrand;
        LastFour = lastFour;
        Expiry = expiry;
        CardholderName = cardholderName;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The owning shopper — the JWT username, same convention as Order.BuyerId.</summary>
    public string BuyerId { get; private set; }

    /// <summary>The PayPal vault/payment-token id used to charge this card later.</summary>
    public string PayPalVaultId { get; private set; }

    // Safe-to-display metadata only — no raw card number or security code exists on this type.
    public string? CardBrand { get; private set; }
    public string? LastFour { get; private set; }
    public string? Expiry { get; private set; }
    public string? CardholderName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
