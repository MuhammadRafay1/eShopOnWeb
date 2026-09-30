using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

/// <summary>
/// A shopper's saved card, represented purely as a PayPal Vault reference (VaultTokenId) plus a
/// safe display descriptor (brand/last4/expiry). The PAN/CVV are never stored here or anywhere else
/// in this application -- PayPal Vault is the PCI-compliant system of record for the card itself,
/// same intent as the (unmapped, unused) Buyer.PaymentMethod comment in the BuyerAggregate.
/// </summary>
public class SavedPaymentMethod : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private SavedPaymentMethod() { }

    public SavedPaymentMethod(string ownerId, string vaultTokenId, string? payPalCustomerId, string? brand, string? last4, string? expiryMonthYear, string? cardholderName)
    {
        Guard.Against.NullOrEmpty(ownerId, nameof(ownerId));
        Guard.Against.NullOrEmpty(vaultTokenId, nameof(vaultTokenId));

        OwnerId = ownerId;
        VaultTokenId = vaultTokenId;
        PayPalCustomerId = payPalCustomerId;
        Brand = brand;
        Last4 = last4;
        ExpiryMonthYear = expiryMonthYear;
        CardholderName = cardholderName;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string OwnerId { get; private set; }
    public string VaultTokenId { get; private set; }
    public string? PayPalCustomerId { get; private set; }
    public string? Brand { get; private set; }
    public string? Last4 { get; private set; }
    public string? ExpiryMonthYear { get; private set; }
    public string? CardholderName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
