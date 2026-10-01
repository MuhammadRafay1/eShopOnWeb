namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>
/// Raw card details for a one-off payment or a card-save. These are passed straight through to PayPal and are
/// NEVER persisted in this app's database nor written to logs.
/// </summary>
public sealed record CardDetails(
    string? Name,
    string Number,
    string Expiry,
    string SecurityCode,
    CardBillingAddress? BillingAddress);

/// <summary>A card billing address. CountryCode is the only field PayPal requires.</summary>
public sealed record CardBillingAddress(
    string? AddressLine1,
    string? AddressLine2,
    string? AdminArea2,
    string? AdminArea1,
    string? PostalCode,
    string CountryCode);
