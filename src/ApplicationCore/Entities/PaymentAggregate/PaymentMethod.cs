using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A shopper's saved card. Standalone aggregate root keyed by <see cref="BuyerId"/> — the same
/// username string <c>Order.BuyerId</c> uses (from the JWT identity), not a foreign key to any Buyer
/// row. Stores only a PayPal vault id and a display-safe summary — never a PAN or CVV.
/// </summary>
public class PaymentMethod : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }
    #pragma warning restore CS8618

    public PaymentMethod(string buyerId, string payPalVaultId, string brand, string lastDigits, string expiry)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(payPalVaultId, nameof(payPalVaultId));

        BuyerId = buyerId;
        PayPalVaultId = payPalVaultId;
        Brand = brand;
        LastDigits = lastDigits;
        Expiry = expiry;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string BuyerId { get; private set; }
    public string PayPalVaultId { get; private set; }  // never exposed over the API
    public string Brand { get; private set; }           // e.g. "VISA"
    public string LastDigits { get; private set; }
    public string Expiry { get; private set; }           // "YYYY-MM"
    public DateTimeOffset CreatedAt { get; private set; }
}
