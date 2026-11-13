using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Credentials and connection settings for Upvest, bound from the <c>Upvest:</c> configuration
/// section. The values live only in configuration (user-secrets / environment / secret store) and
/// are never written into source. <see cref="ClientSecret"/> and <see cref="SigningKeyPassphrase"/>
/// are secrets and are never logged or returned by an endpoint.
/// </summary>
public sealed class UpvestSettings
{
    public const string SectionName = "Upvest";

    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SigningKeyId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Optional: only set when the signing key PEM is encrypted.</summary>
    public string? SigningKeyPassphrase { get; set; }

    [Required(AllowEmptyStrings = false)]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
}
