using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.PaymentModels;

// Shared request/response shapes for the order-payment and saved-card endpoints. Full card details are
// never stored or logged; they travel only on the inbound request and are discarded after the PayPal call.

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

public class CardDto
{
    /// <summary>Card primary account number. Never stored or logged by this application.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>ISO-8601 <c>YYYY-MM</c> expiry.</summary>
    public string Expiry { get; set; } = string.Empty;

    /// <summary>CVV/CVC. Never stored or logged by this application.</summary>
    public string SecurityCode { get; set; } = string.Empty;

    public string? Name { get; set; }

    public BillingAddressDto BillingAddress { get; set; } = new();
}

public class BillingAddressDto
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>2-letter ISO-3166-1 country code (required by PayPal for a card billing address).</summary>
    public string CountryCode { get; set; } = string.Empty;
}

public class OrderLineDto
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class CreateOrderRequest
{
    [JsonIgnore] public string BuyerId { get; set; } = string.Empty;
    public List<OrderLineDto> Items { get; set; } = new();
    public AddressDto ShipToAddress { get; set; } = new();
}

public class PayOrderRequest
{
    [JsonIgnore] public string BuyerId { get; set; } = string.Empty;
    [JsonIgnore] public int OrderId { get; set; }
    public CardDto? Card { get; set; }
    public int? PaymentMethodId { get; set; }
}

public class RefundOrderRequest
{
    [JsonIgnore] public int OrderId { get; set; }
    public decimal? Amount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class SavePaymentMethodRequest
{
    [JsonIgnore] public string BuyerId { get; set; } = string.Empty;
    public CardDto Card { get; set; } = new();
}
