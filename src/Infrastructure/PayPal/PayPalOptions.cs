using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Strongly-typed PayPal settings, bound from the <c>PayPal</c> configuration section. No value is ever
/// hard-coded — the real values come from environment variables loaded into user-secrets at runtime, so
/// the same build runs against a different PayPal account unchanged.
/// </summary>
public class PayPalOptions
{
    public const string SectionName = "PayPal";

    /// <summary>From <c>PAYPAL_CLIENT_ID</c> → <c>PayPal:ClientId</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>From <c>PAYPAL_CLIENT_SECRET</c> → <c>PayPal:ClientSecret</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>From <c>PAYPAL_ENVIRONMENT</c> → <c>PayPal:Environment</c> (must be <c>sandbox</c>).</summary>
    [Required(AllowEmptyStrings = false)]
    public string Environment { get; set; } = string.Empty;

    /// <summary>From <c>PAYPAL_CURRENCY</c> → <c>PayPal:Currency</c> (ISO-4217).</summary>
    [Required(AllowEmptyStrings = false)]
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Optional <c>PayPal:BaseUrl</c> override. When set, used verbatim as the API base address for every
    /// PayPal call (including the OAuth2 token request); when empty, the SDK's sandbox default is used.
    /// </summary>
    public string? BaseUrl { get; set; }
}
