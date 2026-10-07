namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Strongly-typed view of the <c>Upvest:</c> configuration section. Every value
/// is supplied by configuration (user-secrets / environment) — none is
/// hard-coded. The client secret and the signing-key passphrase are secrets and
/// are never logged or returned by any endpoint.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    /// <summary>OAuth 2 client id (also sent as the <c>upvest-client-id</c> header).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth 2 client secret. Secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Identifier of the registered HTTP message signing key.</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Path to the PEM-encoded EC private key used to sign requests.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase protecting the signing key. Secret.</summary>
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest; used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the fund every investment buys.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this host can be reached by Upvest callbacks.</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
