using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.ManagedPortfolio;

/// <summary>
/// The Upvest connection settings, bound from the <c>Upvest</c> configuration section. Every value is
/// <see cref="RequiredAttribute"/>, so the host refuses to start if any one is missing or blank (an empty
/// string fails <c>[Required]</c>). The values are secrets or connection details — never logged, never
/// returned by an endpoint, never written into source. Only the key names appear here.
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

    [Required]
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    [Required]
    public string BaseUrl { get; set; } = string.Empty;
}
