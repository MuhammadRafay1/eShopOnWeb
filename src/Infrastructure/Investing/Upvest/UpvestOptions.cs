namespace Microsoft.eShopWeb.Infrastructure.Investing.Upvest;

/// <summary>
/// Upvest connection settings, bound from the <c>Upvest:</c> configuration section. Values are loaded
/// from configuration (user-secrets / environment) and never hard-coded. <see cref="ClientSecret"/>
/// and <see cref="SigningKeyPassphrase"/> are secrets and are never logged or returned by an endpoint.
/// </summary>
public class UpvestOptions
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest; used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The fund (ETF) the set-aside balance is invested into. An ISIN.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this host can be reached (for webhook callbacks).</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
