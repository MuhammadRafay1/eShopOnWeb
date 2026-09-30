using System;
using System.Text.Json.Serialization;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>Request to save (vault) a card. Card details go straight to PayPal and are never stored.</summary>
public class SavePaymentMethodRequest : BaseRequest
{
    public string? Name { get; set; }
    public string? Number { get; set; }

    /// <summary>Expiry in YYYY-MM format, as the PayPal spec requires.</summary>
    public string? Expiry { get; set; }
    public string? Cvv { get; set; }
    public SaveCardBillingAddressDto? BillingAddress { get; set; }

    [JsonIgnore] public string BuyerId { get; set; } = string.Empty;
}

public class SaveCardBillingAddressDto
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
}

/// <summary>A saved card described safely — enough to recognise it, never full card details.</summary>
public class PaymentMethodDto
{
    public string PaymentMethodId { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public static PaymentMethodDto From(PaymentMethod pm) => new PaymentMethodDto
    {
        PaymentMethodId = pm.PayPalVaultId,
        Brand = pm.Brand,
        Last4 = pm.Last4,
        ExpiryMonth = pm.ExpiryMonth,
        ExpiryYear = pm.ExpiryYear,
        DisplayName = pm.DisplayName,
        CreatedAt = pm.CreatedAt
    };
}
