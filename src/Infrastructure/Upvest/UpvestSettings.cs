namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> configuration section.
/// Values are supplied via .NET user-secrets (loaded from environment variables) and are never
/// hard-coded or committed. <see cref="ClientSecret"/> and <see cref="SigningKeyPassphrase"/> are
/// secrets and must never be logged or returned by an endpoint.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address used verbatim for every call to Upvest.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the fund the set-aside balance is invested in.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this application's PublicApi host can be reached by Upvest.</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
