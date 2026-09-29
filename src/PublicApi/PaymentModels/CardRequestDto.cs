using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

namespace Microsoft.eShopWeb.PublicApi.PaymentModels;

/// <summary>
/// Card details supplied by a caller for a one-off payment or to save a card. Never persisted or
/// logged - only ever mapped to the gateway's <see cref="CardDetails"/> and forwarded to PayPal.
/// </summary>
public class CardRequestDto
{
    /// <summary>Primary account number (13-19 digits).</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Expiry in "YYYY-MM" format.</summary>
    public string Expiry { get; set; } = string.Empty;

    public string SecurityCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BillingAddressDto? BillingAddress { get; set; }

    public CardDetails ToCardDetails() => new(
        Number,
        Expiry,
        SecurityCode,
        Name,
        BillingAddress?.ToCardBillingAddress());
}

public class BillingAddressDto
{
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }

    public CardBillingAddress ToCardBillingAddress() =>
        new(AddressLine1, City, State, PostalCode, CountryCode);
}
