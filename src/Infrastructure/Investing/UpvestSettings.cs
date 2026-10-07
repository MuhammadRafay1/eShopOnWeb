namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> section. The values come from
/// .NET user-secrets / environment configuration; none are hard-coded. <see cref="ClientSecret"/> and
/// <see cref="SigningKeyPassphrase"/> are secrets and must never be logged or returned by an endpoint.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string? SigningKeyPassphrase { get; set; }

    /// <summary>Base address for every call to Upvest; used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The fund (instrument) the shopper's change is invested in.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this application's PublicApi host can be reached by Upvest.</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
