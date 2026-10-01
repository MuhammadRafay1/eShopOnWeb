using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A shopper's saved card, identified by the PayPal vault payment-token id. Carries only what is
/// safe to show the shopper (brand + last 4 + expiry) - never PAN/CVV, which this app never stores.
/// </summary>
public class SavedPaymentMethod : IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private SavedPaymentMethod() { }

    public SavedPaymentMethod(string id, string buyerId, string brand, string lastDigits, string? expiry, string? cardholderName)
    {
        Guard.Against.NullOrEmpty(id, nameof(id));
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        Id = id;
        BuyerId = buyerId;
        Brand = brand ?? "Unknown";
        LastDigits = lastDigits ?? "";
        Expiry = expiry;
        CardholderName = cardholderName;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Primary key - the PayPal vault payment-token id.</summary>
    public string Id { get; private set; }

    public string BuyerId { get; private set; }
    public string Brand { get; private set; }
    public string LastDigits { get; private set; }
    public string? Expiry { get; private set; }
    public string? CardholderName { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
