namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest credentials and endpoint, bound from the <c>Upvest:</c> configuration
/// section (sourced from user-secrets / environment). Secret values are never logged or serialized.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    /// <summary>OAuth client id (also sent as the <c>upvest-client-id</c> header). A UUID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client secret. Secret — never logged or returned.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>The id of the registered HTTP-signature signing key (<c>keyid</c>).</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM-encoded, passphrase-protected EC (P-521) signing key.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase for the signing key. Secret — never logged or returned.</summary>
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every Upvest call (e.g. the sandbox URL). Used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
