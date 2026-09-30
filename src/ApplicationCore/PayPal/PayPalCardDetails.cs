namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

/// <summary>
/// Raw card details for a one-off payment or to save a new card. Never persisted by this application -
/// used only to build the outbound PayPal request and then discarded.
/// </summary>
public record PayPalCardDetails(
    string Number,
    string Expiry, // YYYY-MM
    string SecurityCode,
    string Name,
    PayPalBillingAddress BillingAddress);
