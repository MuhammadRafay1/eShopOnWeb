namespace Microsoft.eShopWeb.PublicApi;

/// <summary>
/// Strongly-typed PayPal settings, bound from the <c>PayPal:</c> configuration section. Values are supplied
/// by configuration/user-secrets/environment — none are hard-coded, so the same build runs against a
/// different PayPal account.
/// </summary>
public class PayPalOptions
{
    public const string CONFIG_NAME = "PayPal";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Environment { get; set; } = "Sandbox";
    public string Currency { get; set; } = "USD";

    /// <summary>Optional base-URL override. When set, it is used verbatim for every PayPal call (including the OAuth token request).</summary>
    public string? BaseUrl { get; set; }
}
