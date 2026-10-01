using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Strongly-typed PayPal settings, bound from the <c>PayPal</c> configuration section. Values are supplied by
/// configuration/user-secrets (ultimately the PAYPAL_* environment variables) and are never hard-coded, so the
/// same build runs against a different PayPal account unchanged.
/// </summary>
public class PayPalOptions
{
    public const string CONFIG_NAME = "PayPal";

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>The PayPal environment. This SDK version only exposes Sandbox; any other value fails startup.</summary>
    public string Environment { get; set; } = "Sandbox";

    [Required]
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Optional base-URL override. When set, it is used verbatim as the API base address for EVERY PayPal call
    /// (including the OAuth2 token request), instead of the environment default.
    /// </summary>
    public string? BaseUrl { get; set; }
}
