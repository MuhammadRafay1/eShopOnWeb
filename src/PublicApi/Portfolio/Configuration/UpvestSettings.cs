using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Configuration;

/// <summary>
/// Strongly-typed Upvest credentials and endpoint, bound from the <c>Upvest:</c> configuration section.
/// The values are supplied through configuration (user-secrets / environment / secret store) and never
/// hard-coded. <see cref="ClientSecret"/> and <see cref="SigningKeyPassphrase"/> are secrets and must never
/// be logged, returned by an endpoint, or written into source.
/// </summary>
public sealed class UpvestSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Upvest";

    /// <summary>OAuth2 client id (also used as the <c>upvest-client-id</c> for request signing).</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret. Secret — never logged or returned.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Identifier of the registered HTTP message-signing key.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM private key used for HTTP message signing.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Passphrase protecting the signing key. Secret — never logged or returned. May legitimately be empty
    /// when the key is not passphrase-protected, so it is intentionally not marked <see cref="RequiredAttribute"/>.
    /// </summary>
    public string? SigningKeyPassphrase { get; set; }

    /// <summary>Base URL used verbatim as the base address for every Upvest call.</summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
}
