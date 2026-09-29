namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

/// <summary>
/// Raw card details for a one-off payment or a card being saved. These never touch eShop's own
/// database or logs - they are only ever forwarded to PayPal over HTTPS.
/// </summary>
public record CardDetails(
    string Number,
    string Expiry,        // "YYYY-MM"
    string SecurityCode,
    string Name,
    CardBillingAddress? BillingAddress);

/// <summary>
/// Billing address for a card, using PayPal's field naming semantics:
/// admin_area_2 = city, admin_area_1 = state/province.
/// </summary>
public record CardBillingAddress(
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? CountryCode);
