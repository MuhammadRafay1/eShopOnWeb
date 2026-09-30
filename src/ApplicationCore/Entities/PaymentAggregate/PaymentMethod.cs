using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A shopper's saved card, described safely enough to recognise it. Never stores the PAN or CVV -
/// only what PayPal's vault returns (brand, last 4, expiry) plus the vault/customer ids needed to
/// pay with it again.
/// </summary>
public class PaymentMethod : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }

    public PaymentMethod(
        string buyerId,
        string payPalVaultId,
        string payPalCustomerId,
        string brand,
        string lastFourDigits,
        string expiryMonthYear,
        string? cardholderName)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));
        Guard.Against.NullOrEmpty(payPalCustomerId, nameof(payPalCustomerId));

        BuyerId = buyerId;
        PayPalVaultId = payPalVaultId;
        PayPalCustomerId = payPalCustomerId;
        Brand = brand ?? "UNKNOWN";
        LastFourDigits = lastFourDigits ?? string.Empty;
        ExpiryMonthYear = expiryMonthYear ?? string.Empty;
        CardholderName = cardholderName;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string BuyerId { get; private set; }
    public string PayPalVaultId { get; private set; }
    public string PayPalCustomerId { get; private set; }
    public string Brand { get; private set; }
    public string LastFourDigits { get; private set; }
    public string ExpiryMonthYear { get; private set; }
    public string? CardholderName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
