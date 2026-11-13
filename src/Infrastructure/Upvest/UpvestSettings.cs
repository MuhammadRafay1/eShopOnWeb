using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> section. The two secrets
/// (<see cref="ClientSecret"/>, <see cref="SigningKeyPassphrase"/>) are read from configuration
/// (user-secrets / a secret store) and are never logged, returned, or written to source.
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

    /// <summary>Optional: the private key may be unencrypted.</summary>
    public string? SigningKeyPassphrase { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = string.Empty;
}
