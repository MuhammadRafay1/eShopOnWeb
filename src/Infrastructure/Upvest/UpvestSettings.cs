namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest credentials and endpoint, bound from the <c>Upvest</c> configuration section.
/// Values are supplied via configuration / user-secrets / environment — never hard-coded.
/// <see cref="ClientSecret"/> and <see cref="SigningKeyPassphrase"/> are secrets and are never logged.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    /// <summary>Client ID issued by Upvest (also sent as the <c>upvest-client-id</c> header).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret issued by Upvest. Secret — never logged or returned.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Key id (<c>keyid</c>) of the registered HTTP message signing key.</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM-encoded EC private signing key.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase protecting the signing key. Secret — never logged or returned.</summary>
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest (e.g. the sandbox or live host).</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
