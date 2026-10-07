namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Upvest connection settings, bound from the <c>Upvest:</c> configuration section. Values are
/// supplied through configuration (user-secrets / environment) and never hard-coded. The client
/// secret and signing-key passphrase are secrets and must never be logged or returned by an endpoint.
/// </summary>
public class UpvestOptions
{
    public const string SectionName = "Upvest";

    /// <summary>OAuth client id; also sent as the <c>upvest-client-id</c> header. A GUID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client secret. Secret — never log or expose.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Id the signing public key is registered under at Upvest.</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM-encoded EC private key used to sign requests.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase for the signing key. Secret — never log or expose.</summary>
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest. Used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the fund the set-aside balance is invested in.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this application can be reached by Upvest (webhooks).</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
