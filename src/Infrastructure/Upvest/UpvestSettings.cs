using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> section. Every value is required so the
/// host refuses to start when one is missing or blank, rather than surfacing it as a 401 on the first call.
/// Secret values (<see cref="ClientSecret"/>, <see cref="SigningKeyPassphrase"/>) are never logged or returned.
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

    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string InstrumentId { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
