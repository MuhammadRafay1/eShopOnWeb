namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Bound from the "PayPal" configuration section. No value is ever hard-coded; the same build
/// must run against a different PayPal account purely by changing configuration.
/// </summary>
public class PayPalOptions
{
    public const string SectionName = "PayPal";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>"sandbox" or "live"; selects the default base URL when <see cref="BaseUrl"/> is unset.</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>ISO-4217 currency used for all amounts sent to PayPal.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Optional override. When set, it is used verbatim as the API base address for every PayPal
    /// call — including the OAuth2 token request — instead of deriving one from the environment.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// The effective base URL: the explicit override if present, otherwise the sandbox or live
    /// host. The sandbox host is the servers[0].url declared in every api-specs/paypal/*.json.
    /// </summary>
    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
            return BaseUrl!.TrimEnd('/');

        var isLive = string.Equals(Environment, "live", System.StringComparison.OrdinalIgnoreCase)
                     || string.Equals(Environment, "production", System.StringComparison.OrdinalIgnoreCase);
        return isLive ? "https://api-m.paypal.com" : "https://api-m.sandbox.paypal.com";
    }
}
