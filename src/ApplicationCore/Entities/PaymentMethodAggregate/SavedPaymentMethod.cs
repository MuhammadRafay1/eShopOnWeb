using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

/// <summary>
/// A shopper's saved (vaulted) card, identified only by PayPal's vault id plus a safe descriptor.
/// The PAN and CVV are never received back from PayPal's vault API and are never stored here.
/// </summary>
public class SavedPaymentMethod : BaseEntity, IAggregateRoot
{
    public string BuyerId { get; private set; }
    public string VaultId { get; private set; }
    public string Brand { get; private set; }
    public string LastDigits { get; private set; }
    public string Expiry { get; private set; }
    public string? CardholderName { get; private set; }
    public string? PayPalCustomerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private SavedPaymentMethod() { }

    public SavedPaymentMethod(
        string buyerId,
        string vaultId,
        string brand,
        string lastDigits,
        string expiry,
        string? cardholderName,
        string? payPalCustomerId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(vaultId, nameof(vaultId));
        Guard.Against.NullOrEmpty(brand, nameof(brand));
        Guard.Against.NullOrEmpty(lastDigits, nameof(lastDigits));
        Guard.Against.NullOrEmpty(expiry, nameof(expiry));

        BuyerId = buyerId;
        VaultId = vaultId;
        Brand = brand;
        LastDigits = lastDigits;
        Expiry = expiry;
        CardholderName = cardholderName;
        PayPalCustomerId = payPalCustomerId;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
