namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Bound from the <c>Upvest:</c> configuration section. Every value is loaded from
/// configuration (user-secrets / environment) — none is hard-coded. The two secrets
/// (<see cref="ClientSecret"/>, <see cref="SigningKeyPassphrase"/>) are never logged or
/// returned by any endpoint.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    /// <summary>OAuth2 client id (also sent as the <c>upvest-client-id</c> header; a UUID).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret. Secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Key id the signing public key is registered under at Upvest (a UUID).</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Path to the PEM private key used for HTTP message signatures.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase protecting the signing key. Secret.</summary>
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest. Used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the fund to invest spare change in.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address this application's PublicApi can be reached at (for webhooks).</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
