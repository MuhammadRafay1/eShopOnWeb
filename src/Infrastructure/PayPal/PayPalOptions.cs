namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Bound from the "PayPal:" configuration section. Values themselves come from user secrets
/// / environment configuration - never hard-coded, never written into this repository.
/// </summary>
public class PayPalOptions
{
    public const string CONFIG_SECTION = "PayPal";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Environment { get; set; } = "sandbox";
    public string Currency { get; set; } = "USD";

    /// <summary>Optional override. When set, used verbatim as the API base address for every PayPal call.</summary>
    public string? BaseUrl { get; set; }

    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
            return BaseUrl.TrimEnd('/');

        return Environment.Trim().ToLowerInvariant() switch
        {
            "live" or "production" => "https://api-m.paypal.com",
            _ => "https://api-m.sandbox.paypal.com"
        };
    }
}
