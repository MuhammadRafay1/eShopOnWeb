using System.ComponentModel.DataAnnotations;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Bound from the "PayPal" configuration section. Values are sourced from environment variables /
/// user-secrets (PAYPAL_CLIENT_ID, PAYPAL_CLIENT_SECRET, PAYPAL_ENVIRONMENT, PAYPAL_CURRENCY) -
/// never hard-coded, never checked into appsettings*.json.
/// </summary>
public class PayPalSettings : IPaymentSettings
{
    public const string CONFIG_NAME = "PayPal";

    [Required(AllowEmptyStrings = false, ErrorMessage = "PayPal:ClientId is not configured.")]
    public string ClientId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "PayPal:ClientSecret is not configured.")]
    public string ClientSecret { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "PayPal:Environment is not configured.")]
    public string Environment { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "PayPal:Currency is not configured.")]
    public string Currency { get; set; } = string.Empty;

    /// <summary>Optional override: when set, used verbatim as the API base address for every PayPal call, including the token request.</summary>
    public string? BaseUrl { get; set; }

    public string CurrencyCode => Currency;
}
