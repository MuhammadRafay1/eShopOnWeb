namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed view of the <c>Upvest:</c> configuration section. Values are supplied through
/// configuration (user-secrets / environment) and are never hard-coded. The two secret values
/// (<see cref="ClientSecret"/> and <see cref="SigningKeyPassphrase"/>) are never logged or returned
/// by any endpoint.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest; used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The instrument (ETF) spare change is invested in.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this application can be reached by Upvest callbacks.</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;

    /// <summary>True when the minimum required settings are present.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(SigningKeyId) &&
        !string.IsNullOrWhiteSpace(SigningKeyPath) &&
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(InstrumentId);
}
