namespace Microsoft.eShopWeb.Infrastructure.Payments.PayPal;

/// <summary>
/// Bound from the "PayPal:" configuration section. No values are ever hard-coded - the same
/// build has to run against a different PayPal account, so everything comes from configuration.
/// </summary>
public class PayPalOptions
{
    public const string SectionName = "PayPal";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>"sandbox" (default) or "live"/"production".</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>ISO 4217 currency code, e.g. "USD".</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Optional explicit base URL. When set, it is used verbatim for EVERY PayPal call, including
    /// the OAuth token request, instead of deriving one from <see cref="Environment"/>.
    /// </summary>
    public string? BaseUrl { get; set; }

    private const string SandboxBaseUrl = "https://api-m.sandbox.paypal.com";
    private const string LiveBaseUrl = "https://api-m.paypal.com";

    /// <summary>
    /// Resolves the API base address per the task's override rule: explicit BaseUrl wins; else
    /// live/production maps to the live host; anything else (default/sandbox) maps to sandbox.
    /// </summary>
    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl.TrimEnd('/');
        }

        var env = (Environment ?? string.Empty).Trim().ToLowerInvariant();
        return env is "live" or "production" ? LiveBaseUrl : SandboxBaseUrl;
    }
}
