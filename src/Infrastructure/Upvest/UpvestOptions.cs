using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed binding of the <c>Upvest:</c> configuration section. Values are supplied from configuration
/// (environment variables / user-secrets) only — never hard-coded. The client secret and signing-key passphrase
/// are secrets and are never logged or returned by any endpoint.
/// </summary>
public class UpvestOptions
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

    /// <summary>The passphrase protecting the signing key. May be empty only if the key is unencrypted.</summary>
    public string? SigningKeyPassphrase { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = string.Empty;
}
