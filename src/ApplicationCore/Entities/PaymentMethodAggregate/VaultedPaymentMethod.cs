using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

/// <summary>
/// A card a shopper saved with PayPal's vault for reuse on a later order. Never carries PAN/CVV - only
/// the safe descriptor PayPal returns (brand, last 4 digits, expiry, cardholder name).
/// </summary>
public class VaultedPaymentMethod : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private VaultedPaymentMethod() { }

    public VaultedPaymentMethod(
        string buyerId,
        string payPalVaultId,
        string? payPalCustomerId,
        string? brand,
        string? lastDigits,
        string? expiry,
        string? cardholderName)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));

        BuyerId = buyerId;
        PayPalVaultId = payPalVaultId;
        PayPalCustomerId = payPalCustomerId;
        Brand = brand;
        LastDigits = lastDigits;
        Expiry = expiry;
        CardholderName = cardholderName;
        CreatedDate = DateTimeOffset.UtcNow;
    }

    public string BuyerId { get; private set; }
    public string PayPalVaultId { get; private set; }
    public string? PayPalCustomerId { get; private set; }
    public string? Brand { get; private set; }
    public string? LastDigits { get; private set; }
    public string? Expiry { get; private set; }
    public string? CardholderName { get; private set; }
    public DateTimeOffset CreatedDate { get; private set; }
}
