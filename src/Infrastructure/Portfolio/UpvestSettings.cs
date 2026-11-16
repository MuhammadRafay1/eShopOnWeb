using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> section. Values come from configuration
/// (user-secrets / environment) and are never hard-coded. Secrets (<see cref="ClientSecret"/>,
/// <see cref="SigningKeyPassphrase"/>) are never logged or returned by any endpoint.
/// </summary>
public sealed class UpvestSettings
{
    public const string SectionName = "Upvest";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Upvest:ClientId is not configured.")]
    public string ClientId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Upvest:ClientSecret is not configured.")]
    public string ClientSecret { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Upvest:SigningKeyId is not configured.")]
    public string SigningKeyId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Upvest:SigningKeyPath is not configured.")]
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Optional — a key may have no passphrase. A blank value is treated as "no passphrase".</summary>
    public string? SigningKeyPassphrase { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Upvest:BaseUrl is not configured.")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The whole-call budget enforced per outbound Upvest call. Defaults to 30s; override with
    /// <c>Upvest:RequestTimeoutSeconds</c>.
    /// </summary>
    [Range(1, 600)]
    public int RequestTimeoutSeconds { get; set; } = 30;
}
