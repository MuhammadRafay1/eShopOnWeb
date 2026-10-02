namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Settings bound from the <c>PayPal:</c> configuration section. Values are sourced from configuration
/// (user-secrets / environment) and never hard-coded, so the same build runs against a different
/// PayPal account.
/// </summary>
public class PayPalSettings
{
    public const string CONFIG_NAME = "PayPal";

    /// <summary>REST client id (from PAYPAL_CLIENT_ID).</summary>
    public string? ClientId { get; set; }

    /// <summary>REST client secret (from PAYPAL_CLIENT_SECRET).</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Target PayPal environment, e.g. "sandbox" (from PAYPAL_ENVIRONMENT).</summary>
    public string? Environment { get; set; }

    /// <summary>ISO-4217 currency for all amounts (from PAYPAL_CURRENCY).</summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Optional base-URL override. When set, it is used verbatim as the API base address for every
    /// PayPal call, including the OAuth token request.
    /// </summary>
    public string? BaseUrl { get; set; }
}
