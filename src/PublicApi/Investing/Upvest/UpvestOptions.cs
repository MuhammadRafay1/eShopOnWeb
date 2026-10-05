namespace Microsoft.eShopWeb.PublicApi.Investing.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest</c> configuration section.
/// Secret values (client secret, signing-key passphrase) are supplied via .NET user-secrets /
/// environment and are never hard-coded or logged.
/// </summary>
public class UpvestOptions
{
    public const string ConfigSection = "Upvest";

    /// <summary>OAuth2 client id; also sent as the <c>upvest-client-id</c> header.</summary>
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Key id (<c>keyid</c>) of the registered HTTP message signing key.</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Path to the PEM-encoded (encrypted) EC P-521 private signing key.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest; used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the fund invested into.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this app's PublicApi host is reachable (for webhooks).</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
