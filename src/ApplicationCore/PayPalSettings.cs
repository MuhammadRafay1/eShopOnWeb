using System;

namespace Microsoft.eShopWeb.ApplicationCore;

/// <summary>
/// Bound from the "PayPal:" configuration section. Values always come from environment variables
/// (PAYPAL_CLIENT_ID / PAYPAL_CLIENT_SECRET / PAYPAL_ENVIRONMENT / PAYPAL_CURRENCY, mapped to these
/// keys at startup) or user-secrets in Development -- never hard-coded, so the same build can run
/// against a different PayPal account.
/// </summary>
public class PayPalSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;

    /// <summary>Optional override. When set, used verbatim as the API base for every PayPal call, including the token endpoint.</summary>
    public string? BaseUrl { get; set; }

    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl!.TrimEnd('/');
        }

        return Environment.Equals("live", StringComparison.OrdinalIgnoreCase) || Environment.Equals("production", StringComparison.OrdinalIgnoreCase)
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";
    }
}
