using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Binds the <c>PayPal:</c> configuration section. Values are supplied at runtime (user-secrets /
/// environment) and are never written into any file in the repository.
/// </summary>
public class PayPalOptions : IPayPalCurrencyProvider
{
    public const string ConfigSection = "PayPal";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>"sandbox" | "live". Determines the derived base URL when BaseUrl is not set.</summary>
    public string Environment { get; set; } = "sandbox";

    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Optional base-URL override. When set, it is used verbatim for EVERY PayPal call (including
    /// the OAuth2 token request) instead of a URL derived from <see cref="Environment"/>.
    /// </summary>
    public string? BaseUrl { get; set; }
}
