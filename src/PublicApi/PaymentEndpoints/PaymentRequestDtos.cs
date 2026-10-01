using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>Card details supplied in a request. Passed straight to PayPal; never persisted or logged by this app.</summary>
public class CardInput
{
    public string? Name { get; set; }
    public string Number { get; set; } = string.Empty;

    /// <summary>Expiry in PayPal's YYYY-MM format (e.g. 2030-01).</summary>
    public string Expiry { get; set; } = string.Empty;
    public string SecurityCode { get; set; } = string.Empty;
    public BillingAddressInput? BillingAddress { get; set; }

    public CardDetails ToCardDetails() => new(
        Name,
        Number,
        Expiry,
        SecurityCode,
        BillingAddress?.ToDomain());
}

public class BillingAddressInput
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }

    /// <summary>City (PayPal admin_area_2).</summary>
    public string? City { get; set; }

    /// <summary>State/province (PayPal admin_area_1).</summary>
    public string? State { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>Two-letter country code (required by PayPal).</summary>
    public string CountryCode { get; set; } = string.Empty;

    public CardBillingAddress ToDomain() => new(
        AddressLine1,
        AddressLine2,
        City,
        State,
        PostalCode,
        CountryCode);
}
