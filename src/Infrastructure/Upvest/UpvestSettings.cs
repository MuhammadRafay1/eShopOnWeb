using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest credentials and endpoint, bound from the <c>Upvest:</c> configuration section.
/// The values arrive from user-secrets / environment; nothing is hard-coded. The two secret members
/// (<see cref="ClientSecret"/>, <see cref="SigningKeyPassphrase"/>) are never logged or returned.
/// </summary>
public class UpvestSettings
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

    /// <summary>The signing key may be unencrypted; a passphrase is required here only because this
    /// deployment's key is encrypted. Still validated non-empty to fail fast on a mis-provisioned secret.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
}
