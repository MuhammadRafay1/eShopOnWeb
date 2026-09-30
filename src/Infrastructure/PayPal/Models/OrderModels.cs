using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal.Models;

public class BillingAddress
{
    [JsonPropertyName("address_line_1")]
    public string? AddressLine1 { get; set; }

    [JsonPropertyName("address_line_2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("admin_area_2")]
    public string? AdminArea2 { get; set; }

    [JsonPropertyName("admin_area_1")]
    public string? AdminArea1 { get; set; }

    [JsonPropertyName("postal_code")]
    public string? PostalCode { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }
}

public class CardRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("expiry")]
    public string? Expiry { get; set; }

    [JsonPropertyName("security_code")]
    public string? SecurityCode { get; set; }

    [JsonPropertyName("billing_address")]
    public BillingAddress? BillingAddress { get; set; }

    [JsonPropertyName("vault_id")]
    public string? VaultId { get; set; }
}

public class PaymentSourceRequest
{
    [JsonPropertyName("card")]
    public CardRequest? Card { get; set; }
}

public class AmountRequest
{
    [JsonPropertyName("currency_code")]
    public string CurrencyCode { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class PurchaseUnitRequest
{
    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("custom_id")]
    public string? CustomId { get; set; }

    [JsonPropertyName("amount")]
    public AmountRequest Amount { get; set; } = new();
}

public class OrderRequest
{
    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "AUTHORIZE";

    [JsonPropertyName("purchase_units")]
    public List<PurchaseUnitRequest> PurchaseUnits { get; set; } = new();

    [JsonPropertyName("payment_source")]
    public PaymentSourceRequest? PaymentSource { get; set; }
}

public class CardResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("last_digits")]
    public string? LastDigits { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("expiry")]
    public string? Expiry { get; set; }

    [JsonPropertyName("authentication_result")]
    public AuthenticationResult? AuthenticationResult { get; set; }
}

public class AuthenticationResult
{
    [JsonPropertyName("three_d_secure")]
    public ThreeDSecureResult? ThreeDSecure { get; set; }
}

public class ThreeDSecureResult
{
    [JsonPropertyName("authentication_status")]
    public string? AuthenticationStatus { get; set; }
}

public class PaymentSourceResponse
{
    [JsonPropertyName("card")]
    public CardResponse? Card { get; set; }
}

public class SellerReceivableBreakdown
{
    [JsonPropertyName("gross_amount")]
    public Money GrossAmount { get; set; } = new();

    [JsonPropertyName("paypal_fee")]
    public Money? PayPalFee { get; set; }

    [JsonPropertyName("net_amount")]
    public Money? NetAmount { get; set; }
}

public class Capture
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("seller_receivable_breakdown")]
    public SellerReceivableBreakdown? SellerReceivableBreakdown { get; set; }
}

public class Authorization
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public Money? Amount { get; set; }

    [JsonPropertyName("expiration_time")]
    public System.DateTimeOffset? ExpirationTime { get; set; }
}

public class PaymentCollection
{
    [JsonPropertyName("authorizations")]
    public List<Authorization>? Authorizations { get; set; }

    [JsonPropertyName("captures")]
    public List<Capture>? Captures { get; set; }
}

public class PurchaseUnit
{
    [JsonPropertyName("invoice_id")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("payments")]
    public PaymentCollection? Payments { get; set; }
}

public class LinkDescription
{
    [JsonPropertyName("rel")]
    public string? Rel { get; set; }

    [JsonPropertyName("href")]
    public string? Href { get; set; }
}

public class OrderResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("payment_source")]
    public PaymentSourceResponse? PaymentSource { get; set; }

    [JsonPropertyName("purchase_units")]
    public List<PurchaseUnit>? PurchaseUnits { get; set; }

    [JsonPropertyName("links")]
    public List<LinkDescription>? Links { get; set; }
}
