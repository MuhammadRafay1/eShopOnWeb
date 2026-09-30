using System.ComponentModel.DataAnnotations;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>
/// Raw card details supplied for a one-off payment or to be vaulted. Never persisted or logged by
/// this application — passed straight to PayPal.
/// </summary>
public class CardModel
{
    [Required] public string Number { get; set; } = string.Empty;

    /// <summary>Card expiry in "YYYY-MM" format (PayPal's format).</summary>
    [Required] public string Expiry { get; set; } = string.Empty;

    [Required] public string SecurityCode { get; set; } = string.Empty;
    [Required] public string Name { get; set; } = string.Empty;

    [Required] public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    [Required] public string City { get; set; } = string.Empty;
    [Required] public string State { get; set; } = string.Empty;
    [Required] public string PostalCode { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code, e.g. "US".</summary>
    [Required] public string CountryCode { get; set; } = string.Empty;

    public PaymentCard ToPaymentCard() => new(
        Number, Expiry, SecurityCode, Name,
        AddressLine1, AddressLine2, City, State, PostalCode, CountryCode);
}

/// <summary>Optional shipping address for an order; defaults to a placeholder when omitted.</summary>
public class AddressModel
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

/// <summary>Safe, non-sensitive description of a saved card returned to the shopper.</summary>
public class SavedCardModel
{
    public int PaymentMethodId { get; set; }
    public string CardBrand { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
}
