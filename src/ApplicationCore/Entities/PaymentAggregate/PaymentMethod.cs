using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A card the shopper has saved (vaulted with PayPal) for reuse on later orders. Its
/// <see cref="BaseEntity.Id"/> is the <c>paymentMethodId</c>. Only a safe, non-sensitive
/// description of the card is stored (brand / last four / expiry) — never the full card number.
/// The actual card lives in PayPal's vault, referenced by <see cref="PayPalVaultId"/>.
/// </summary>
public class PaymentMethod : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }

    public PaymentMethod(string buyerId, string payPalVaultId, string? payPalCustomerId,
        string cardBrand, string last4, string expiry)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));
        Guard.Against.NullOrEmpty(last4, nameof(last4));

        BuyerId = buyerId;
        PayPalVaultId = payPalVaultId;
        PayPalCustomerId = payPalCustomerId;
        CardBrand = cardBrand;
        Last4 = last4;
        Expiry = expiry;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Owning shopper — same identity string convention as Order.BuyerId (JWT name claim).</summary>
    public string BuyerId { get; private set; }

    /// <summary>The vault payment-token id, used as payment_source.card.vault_id when paying.</summary>
    public string PayPalVaultId { get; private set; }

    /// <summary>PayPal's generated customer id, kept for reference/diagnostics.</summary>
    public string? PayPalCustomerId { get; private set; }

    public string CardBrand { get; private set; }
    public string Last4 { get; private set; }

    /// <summary>Expiry in PayPal's "YYYY-MM" format.</summary>
    public string Expiry { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
