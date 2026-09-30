using System;
using System.Text.RegularExpressions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi;

/// <summary>
/// The wire shape a caller supplies for a one-off card payment or a card save. Validated against
/// the same patterns api-specs/paypal declares for card_request/vault card_request before it is
/// ever sent to PayPal. Never logged, never persisted as-is.
/// </summary>
public class CardRequestDto
{
    public string Number { get; init; } = string.Empty;
    public string Expiry { get; init; } = string.Empty;
    public string SecurityCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string? State { get; init; }
    public string PostalCode { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;

    private static readonly Regex NumberPattern = new("^[0-9]{13,19}$", RegexOptions.Compiled);
    private static readonly Regex ExpiryPattern = new("^[0-9]{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);
    private static readonly Regex CvvPattern = new("^[0-9]{3,4}$", RegexOptions.Compiled);

    public CardDetails ToCardDetails()
    {
        if (string.IsNullOrWhiteSpace(Number) || !NumberPattern.IsMatch(Number))
        {
            throw new ArgumentException("card.number must be 13-19 digits.");
        }
        if (string.IsNullOrWhiteSpace(Expiry) || !ExpiryPattern.IsMatch(Expiry))
        {
            throw new ArgumentException("card.expiry must be in YYYY-MM format.");
        }
        if (string.IsNullOrWhiteSpace(SecurityCode) || !CvvPattern.IsMatch(SecurityCode))
        {
            throw new ArgumentException("card.securityCode must be 3-4 digits.");
        }
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("card.name is required.");
        }
        if (string.IsNullOrWhiteSpace(CountryCode))
        {
            throw new ArgumentException("card.billingAddress.countryCode is required.");
        }

        return new CardDetails(Number, Expiry, SecurityCode, Name, AddressLine1, AddressLine2, City, State, PostalCode, CountryCode);
    }
}
