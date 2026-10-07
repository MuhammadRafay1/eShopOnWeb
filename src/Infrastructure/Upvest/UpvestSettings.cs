namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Bound from the <c>Upvest</c> configuration section. The values are supplied through configuration
/// (user-secrets / environment) and are never hard-coded; the client secret and signing-key passphrase
/// are secrets and are never logged or returned by an endpoint.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string? SigningKeyPassphrase { get; set; }

    /// <summary>The base address for every call to Upvest; used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The fund (instrument) the spare change is invested in.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>The public base address at which this host can be reached by Upvest callbacks.</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
