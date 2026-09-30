using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.Infrastructure.PayPal.Models;

public class Money
{
    [JsonPropertyName("currency_code")]
    public string CurrencyCode { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
