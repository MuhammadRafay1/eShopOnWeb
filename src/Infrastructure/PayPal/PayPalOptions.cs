using System;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

public class PayPalOptions
{
    public const string CONFIG_SECTION = "PayPal";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Environment { get; set; } = "sandbox";
    public string Currency { get; set; } = "USD";
    public string? BaseUrl { get; set; }

    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl.TrimEnd('/');
        }

        return Environment.Trim().ToLowerInvariant() switch
        {
            "live" or "production" => "https://api-m.paypal.com",
            _ => "https://api-m.sandbox.paypal.com"
        };
    }

    public int CurrencyDecimalPlaces()
    {
        // Zero-decimal currencies per PayPal's supported currency list.
        var zeroDecimalCurrencies = new[] { "JPY", "HUF", "TWD" };
        return Array.IndexOf(zeroDecimalCurrencies, Currency.ToUpperInvariant()) >= 0 ? 0 : 2;
    }
}
