using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.PublicApi.Portfolio;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> section. The values are
/// supplied per deployment (user-secrets / environment / a secret store) and are never written into
/// the repository. <see cref="ClientSecret"/> and <see cref="SigningKeyPassphrase"/> are secrets and
/// are never logged or returned by any endpoint.
/// </summary>
public sealed class UpvestSettings
{
    public const string SectionName = "Upvest";

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    [Required]
    public string SigningKeyId { get; set; } = string.Empty;

    [Required]
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase for the signing key; optional when the key is unencrypted.</summary>
    public string? SigningKeyPassphrase { get; set; }

    /// <summary>Base address for every Upvest call; used verbatim.</summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional sandbox collection IBAN that receives a simulated inbound transfer when settling a
    /// top-up in the sandbox. Only used by the test funding primitive; defaulted when unset.
    /// </summary>
    public string? SandboxCollectionIban { get; set; }
}
