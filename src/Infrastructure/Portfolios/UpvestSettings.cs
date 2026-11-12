namespace Microsoft.eShopWeb.Infrastructure.Portfolios;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> configuration section.
/// Values are supplied through .NET user-secrets / environment configuration and are never
/// hard-coded. The client secret and signing-key passphrase are secrets and must never be logged.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    /// <summary>OAuth2 client id, and the value of the <c>upvest-client-id</c> header. (Upvest:ClientId)</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret. Secret — never log. (Upvest:ClientSecret)</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Key id of the registered HTTP signing key, used as the signature <c>keyid</c>. (Upvest:SigningKeyId)</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM-encoded EC signing private key. (Upvest:SigningKeyPath)</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase protecting the signing key. Secret — never log. (Upvest:SigningKeyPassphrase)</summary>
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest. (Upvest:BaseUrl)</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
