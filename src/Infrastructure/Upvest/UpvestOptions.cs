using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Binds the <c>Upvest:</c> configuration section. Values come from configuration (user-secrets in dev,
/// environment / secret store in prod) — never hard-coded. <see cref="ClientSecret"/> and
/// <see cref="SigningKeyPassphrase"/> are secrets and are never logged or returned by any endpoint.
/// </summary>
public class UpvestOptions
{
    public const string ConfigSection = "Upvest";

    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SigningKeyId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase for the signing key. May be empty only when the key is unencrypted.</summary>
    public string? SigningKeyPassphrase { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = string.Empty;
}
