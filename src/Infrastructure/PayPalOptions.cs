namespace Microsoft.eShopWeb.Infrastructure;

/// <summary>
/// Strongly-typed PayPal settings, bound from the <c>PayPal:</c> configuration section. No value is
/// ever hard-coded — these are loaded from user-secrets / environment configuration.
/// </summary>
public class PayPalOptions
{
    public const string ConfigSectionName = "PayPal";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// From PAYPAL_ENVIRONMENT. Informational: this SDK exposes only a Sandbox server member, so any
    /// non-sandbox host must be reached via <see cref="BaseUrl"/>.
    /// </summary>
    public string Environment { get; set; } = "sandbox";

    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Optional API base-URL override. When set, it is used verbatim for every PayPal call — including
    /// the OAuth2 token request — instead of the SDK's default sandbox host.
    /// </summary>
    public string? BaseUrl { get; set; }
}
