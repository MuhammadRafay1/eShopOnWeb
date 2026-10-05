namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Settings for talking to Upvest, bound from the "Upvest" configuration
/// section. Values are supplied out of band (environment / user-secrets) and
/// are never hard-coded. The client secret and signing-key passphrase are
/// secrets: never log them or return them from an endpoint.
/// </summary>
public class UpvestOptions
{
    public const string CONFIG_NAME = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest; used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the fund the shopper's change is invested in.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this app's PublicApi is reachable (for webhooks).</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
