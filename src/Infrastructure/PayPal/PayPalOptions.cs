namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// PayPal settings bound from the <c>PayPal:</c> configuration section. No value is hard-coded;
/// credentials are supplied through configuration (bridged from the PAYPAL_* environment
/// variables) so the same build can run against a different PayPal account.
/// </summary>
public class PayPalOptions
{
    public const string ConfigSection = "PayPal";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Environment { get; set; } = "Sandbox";
    public string Currency { get; set; } = "USD";

    /// <summary>Optional API base-URL override. When set, it is used verbatim for every PayPal call
    /// (including the OAuth token request); otherwise the base URL is derived from
    /// <see cref="Environment"/>.</summary>
    public string? BaseUrl { get; set; }
}
