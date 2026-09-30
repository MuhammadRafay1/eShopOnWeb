namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

/// <summary>
/// Billing address for a card presented directly to PayPal (one-off payment or vaulting). Distinct from
/// the order's <see cref="Entities.OrderAggregate.Address"/> shipping address.
/// </summary>
public record PayPalBillingAddress(
    string Line1,
    string City,
    string? State,
    string PostalCode,
    string CountryCode);
