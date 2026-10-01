using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;

/// <summary>
/// A card a shopper has saved for reuse. The actual card data lives only in PayPal Vault (a PCI-compliant
/// system) — this app stores the PayPal payment-token id plus a few safe display fields, never the PAN/CVV.
/// </summary>
public class PaymentMethod : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }

    public PaymentMethod(string alias, string payPalPaymentTokenId, string? last4, string? brand, string? expiry)
    {
        Guard.Against.NullOrEmpty(payPalPaymentTokenId, nameof(payPalPaymentTokenId));

        Alias = alias;
        CardId = payPalPaymentTokenId;
        Last4 = last4;
        Brand = brand;
        Expiry = expiry;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string? Alias { get; private set; }

    /// <summary>The PayPal Vault payment-token id. Actual card data is stored in PayPal (PCI-compliant), never here.</summary>
    public string? CardId { get; private set; }

    public string? Last4 { get; private set; }

    /// <summary>PayPal's card brand wire value (e.g. VISA, MASTERCARD).</summary>
    public string? Brand { get; private set; }

    /// <summary>Card expiry in YYYY-MM form, as returned by PayPal.</summary>
    public string? Expiry { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // Soft delete: keeps Payment/Order history unaffected and makes a repeated DELETE an idempotent no-op.
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public void MarkDeleted()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
