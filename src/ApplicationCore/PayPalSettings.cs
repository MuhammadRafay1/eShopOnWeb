using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.ApplicationCore;

/// <summary>
/// Bound from the "PayPal:" configuration section. Every property is validated present and non-blank
/// at startup (see AddPayPalIntegration) so a missing credential fails fast rather than surfacing as a
/// 401 on the first live call.
/// </summary>
public class PayPalSettings
{
    public const string CONFIG_NAME = "PayPal";

    [Required, MinLength(1)]
    public string ClientId { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string ClientSecret { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string Environment { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string Currency { get; set; } = string.Empty;

    /// <summary>Optional. When set, used verbatim as the API base address for every PayPal call, including the token request.</summary>
    public string? BaseUrl { get; set; }
}
