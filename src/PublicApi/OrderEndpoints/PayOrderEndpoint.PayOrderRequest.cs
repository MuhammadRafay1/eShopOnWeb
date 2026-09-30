using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Pays an order with EITHER a one-off card OR a named saved card — exactly one must be supplied.
/// </summary>
public class PayOrderRequest : BaseRequest
{
    /// <summary>Full card details for a one-off payment. Sent straight to PayPal, never stored.</summary>
    public CardDto? Card { get; set; }

    /// <summary>Names one of the shopper's saved cards to pay with instead.</summary>
    public string? PaymentMethodId { get; set; }

    [JsonIgnore] public int OrderId { get; set; }
    [JsonIgnore] public string BuyerId { get; set; } = string.Empty;
}

public class CardDto
{
    public string? Name { get; set; }
    public string? Number { get; set; }

    /// <summary>Expiry in YYYY-MM format, as the PayPal spec requires.</summary>
    public string? Expiry { get; set; }
    public string? Cvv { get; set; }
    public CardBillingAddressDto? BillingAddress { get; set; }
}

public class CardBillingAddressDto
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
}
