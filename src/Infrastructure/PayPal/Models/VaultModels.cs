using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal.Models;

public class VaultCardRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("expiry")]
    public string Expiry { get; set; } = string.Empty;

    [JsonPropertyName("security_code")]
    public string? SecurityCode { get; set; }

    [JsonPropertyName("billing_address")]
    public BillingAddress? BillingAddress { get; set; }
}

public class VaultPaymentSourceRequest
{
    [JsonPropertyName("card")]
    public VaultCardRequest Card { get; set; } = new();
}

public class VaultCustomer
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("merchant_customer_id")]
    public string? MerchantCustomerId { get; set; }
}

public class PaymentTokenRequest
{
    [JsonPropertyName("customer")]
    public VaultCustomer? Customer { get; set; }

    [JsonPropertyName("payment_source")]
    public VaultPaymentSourceRequest PaymentSource { get; set; } = new();
}

public class VaultCardResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("last_digits")]
    public string? LastDigits { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("expiry")]
    public string? Expiry { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class VaultPaymentSourceResponse
{
    [JsonPropertyName("card")]
    public VaultCardResponse? Card { get; set; }
}

public class PaymentTokenResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("customer")]
    public VaultCustomer? Customer { get; set; }

    [JsonPropertyName("payment_source")]
    public VaultPaymentSourceResponse? PaymentSource { get; set; }
}
