namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Configuration for the Upvest Investment API integration, bound from the "Upvest" section.
/// Every value is supplied through configuration (user-secrets / environment) — none is
/// hard-coded. <see cref="ClientSecret"/> and <see cref="SigningKeyPassphrase"/> are secrets and
/// must never be logged or returned by an endpoint.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    /// <summary>OAuth2 client id (also sent as the <c>upvest-client-id</c> header). A UUID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret. Secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Key id of the registered HTTP-message-signing key (the signature <c>keyid</c>).</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM-encoded, passphrase-protected EC signing private key.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase that decrypts the signing private key. Secret.</summary>
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest. Used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the fund that set-aside change is invested into.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this application can be reached by Upvest webhooks.</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
