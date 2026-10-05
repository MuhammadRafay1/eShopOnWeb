namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> configuration section.
/// Values are supplied via .NET user-secrets / environment; none are hard-coded.
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

    /// <summary>The fund (instrument) that set-aside change is invested into.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this application can be reached by Upvest (webhooks).</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
