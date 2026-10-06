using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Configuration for the Upvest integration, bound from the <c>Upvest</c> section. Every value is supplied
/// by deployment configuration (environment / user-secrets) — none is hard-coded. The host refuses to start
/// if any is missing or blank (see <see cref="UpvestStartupValidator"/>).
/// </summary>
public sealed class UpvestSettings
{
    public const string SectionName = "Upvest";

    /// <summary>OAuth2 client id / tenant id (UUID). Sent as both <c>client_id</c> and <c>upvest-client-id</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret. Secret — never logged or returned.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Key id of the registered HTTP-signature key (becomes the signature <c>keyid</c>).</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM private key used for HTTP message signing.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase decrypting the private key. Secret — never logged or returned.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest. Used verbatim.</summary>
    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the exchange-traded fund set-aside change is invested in.</summary>
    [Required(AllowEmptyStrings = false)]
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this application can be reached by Upvest (for webhooks).</summary>
    [Required(AllowEmptyStrings = false)]
    public string CallbackBaseUrl { get; set; } = string.Empty;
}
