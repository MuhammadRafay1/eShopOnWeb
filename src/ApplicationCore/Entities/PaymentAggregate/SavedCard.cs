using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A card a shopper has saved for reuse. Full card details are never stored here — only PayPal's
/// vault id plus a safe description (brand + last four + expiry) the shopper can recognise. The card
/// belongs to the shopper who saved it; one shopper must never see, use, or delete another's.
/// </summary>
public class SavedCard : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private SavedCard() { }

    public SavedCard(string buyerId, string payPalVaultId, string payPalCustomerId,
        string? brand, string? lastFourDigits, string? expiry, string? cardholderName)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));
        Guard.Against.NullOrEmpty(payPalCustomerId, nameof(payPalCustomerId));

        BuyerId = buyerId;
        PayPalVaultId = payPalVaultId;
        PayPalCustomerId = payPalCustomerId;
        Brand = brand;
        LastFourDigits = lastFourDigits;
        Expiry = expiry;
        CardholderName = cardholderName;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string BuyerId { get; private set; }

    /// <summary>PayPal's vault id for the stored card; used as payment_source.card.vault_id when paying.</summary>
    public string PayPalVaultId { get; private set; }

    /// <summary>The PayPal customer the card is vaulted under; shared by all of this shopper's cards.</summary>
    public string PayPalCustomerId { get; private set; }

    public string? Brand { get; private set; }
    public string? LastFourDigits { get; private set; }
    public string? Expiry { get; private set; }
    public string? CardholderName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
