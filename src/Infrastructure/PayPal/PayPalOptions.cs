using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Bound from the "PayPal" configuration section. Values come from environment variables
/// (PAYPAL_CLIENT_ID, PAYPAL_CLIENT_SECRET, PAYPAL_ENVIRONMENT, PAYPAL_CURRENCY) loaded into user-secrets
/// — never hard-coded. <see cref="BaseUrl"/> is an optional override, used verbatim for every PayPal call
/// (including the credential/token request) when set.
/// </summary>
public class PayPalOptions
{
    public const string ConfigSectionName = "PayPal";

    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Environment { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Currency { get; set; } = string.Empty;

    public string? BaseUrl { get; set; }
}
