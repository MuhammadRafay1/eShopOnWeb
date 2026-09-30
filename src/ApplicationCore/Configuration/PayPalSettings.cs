namespace Microsoft.eShopWeb.ApplicationCore.Configuration;

/// <summary>
/// Bound from the "PayPal" configuration section. Values arrive via environment
/// variables (PAYPAL_CLIENT_ID, PAYPAL_CLIENT_SECRET, PAYPAL_ENVIRONMENT, PAYPAL_CURRENCY)
/// mapped onto these keys at startup - never hard-code real values here.
/// </summary>
public class PayPalSettings
{
    public const string SECTION_NAME = "PayPal";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Optional override. When set, used verbatim as the base address for every PayPal call,
    /// including the OAuth token request, instead of deriving one from Environment.
    /// </summary>
    public string? BaseUrl { get; set; }
}
