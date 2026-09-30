namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Raw card details for a one-off or vaulting payment source. Never persisted by this application;
/// it exists only long enough to be forwarded to PayPal and then discarded.
/// </summary>
public record CardDetails(
    string Name,
    string Number,
    string Expiry,
    string SecurityCode,
    PayPalBillingAddress BillingAddress);

public record PayPalBillingAddress(
    string AddressLine1,
    string? AddressLine2,
    string AdminArea2,
    string AdminArea1,
    string PostalCode,
    string CountryCode);
