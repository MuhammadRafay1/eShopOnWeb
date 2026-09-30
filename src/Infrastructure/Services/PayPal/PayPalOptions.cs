namespace Microsoft.eShopWeb.Infrastructure.Services.PayPal;

/// <summary>
/// Bound from the <c>PayPal:</c> configuration section. No values are hard-coded — they
/// come from user-secrets / environment (see Program.cs env-var bridge).
/// </summary>
public class PayPalOptions
{
    public const string SectionName = "PayPal";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>"sandbox" or "live"/"production".</summary>
    public string Environment { get; set; } = "sandbox";

    /// <summary>ISO-4217 currency code used for all amounts, e.g. "USD".</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Optional explicit API base address. When set (non-empty) it is used verbatim for
    /// every PayPal call including the token request; otherwise the base is derived from
    /// <see cref="Environment"/>.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>Resolve the effective API base URL per the required precedence.</summary>
    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl!.TrimEnd('/');
        }

        return Environment?.Trim().ToLowerInvariant() switch
        {
            "sandbox" => "https://api-m.sandbox.paypal.com",
            "live" or "production" => "https://api-m.paypal.com",
            _ => throw new System.InvalidOperationException(
                $"PayPal:Environment '{Environment}' is not valid. Use 'sandbox' or 'live', or set PayPal:BaseUrl explicitly.")
        };
    }
}
