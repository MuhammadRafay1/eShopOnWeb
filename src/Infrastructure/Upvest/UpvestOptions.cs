namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Upvest credentials and connection settings, bound from the <c>Upvest:</c> configuration
/// section. The values are supplied via environment / user-secrets and are never hard-coded.
/// </summary>
public class UpvestOptions
{
    public const string SectionName = "Upvest";

    /// <summary>OAuth2 client id (also sent as the <c>upvest-client-id</c> header).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret. Secret: never logged or returned.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>The <c>keyid</c> of the registered HTTP-message-signing key.</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM-encoded EC signing private key.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase protecting the signing key. Secret: never logged or returned.</summary>
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest (e.g. the sandbox URL). Used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
