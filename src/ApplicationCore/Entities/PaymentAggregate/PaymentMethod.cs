using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A card a shopper has saved for reuse. The card itself lives only in PayPal's Vault;
/// this row keeps the durable Vault payment-token id plus the safe descriptors
/// (brand / last four / expiry) needed to recognise the card. No PAN or CVV is stored.
/// Owned by the shopper who saved it (<see cref="BuyerId"/>).
/// </summary>
public class PaymentMethod : BaseEntity, IAggregateRoot
{
    /// <summary>Owner — the same identity string used as Order.BuyerId (ClaimTypes.Name).</summary>
    public string BuyerId { get; private set; }

    /// <summary>The PayPal Vault payment-token id (a.k.a. vault_id) used to charge the card later.</summary>
    public string PayPalPaymentTokenId { get; private set; }

    public string? CardBrand { get; private set; }
    public string? LastDigits { get; private set; }

    /// <summary>Card expiry in PayPal's "YYYY-MM" format.</summary>
    public string? ExpiryMonthYear { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

#pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }
#pragma warning restore CS8618

    public PaymentMethod(string buyerId, string payPalPaymentTokenId, string? cardBrand, string? lastDigits, string? expiryMonthYear)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalPaymentTokenId, nameof(payPalPaymentTokenId));

        BuyerId = buyerId;
        PayPalPaymentTokenId = payPalPaymentTokenId;
        CardBrand = cardBrand;
        LastDigits = lastDigits;
        ExpiryMonthYear = expiryMonthYear;
    }
}
