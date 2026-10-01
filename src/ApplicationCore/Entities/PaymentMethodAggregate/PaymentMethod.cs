using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

/// <summary>
/// A card a shopper has saved (vaulted at PayPal) for reuse. Its own small aggregate, owned by the
/// shopper who saved it (<see cref="BuyerId"/>). The application database never holds full card
/// details — only PayPal's own masked description (brand, last four digits, expiry) plus the opaque
/// vault and customer ids needed to pay with it or delete it later.
/// </summary>
public class PaymentMethod : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }
#pragma warning restore CS8618

    public PaymentMethod(string buyerId, string payPalVaultId, string payPalCustomerId, string? brand, string? lastDigits, string? expiry)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));
        Guard.Against.NullOrEmpty(payPalCustomerId, nameof(payPalCustomerId));

        BuyerId = buyerId;
        PayPalVaultId = payPalVaultId;
        PayPalCustomerId = payPalCustomerId;
        Brand = brand;
        LastDigits = lastDigits;
        Expiry = expiry;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string BuyerId { get; private set; }

    /// <summary>The PayPal-generated vault id used as <c>payment_source.card.vault_id</c> when paying.</summary>
    public string PayPalVaultId { get; private set; }

    /// <summary>The PayPal-generated customer id, reused across this shopper's vault calls.</summary>
    public string PayPalCustomerId { get; private set; }

    public string? Brand { get; private set; }
    public string? LastDigits { get; private set; }
    public string? Expiry { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
