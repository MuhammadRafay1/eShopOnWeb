using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest</c> configuration section.
/// Every value is supplied by configuration (user-secrets / environment) — none is hard-coded.
/// </summary>
public sealed class UpvestOptions
{
    public const string SectionName = "Upvest";

    /// <summary>Client ID issued by Upvest (a UUID). Not a secret.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client secret. Secret — never logged or returned.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Key id of the HTTP-message-signature signing key (a UUID).</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM-encoded, passphrase-encrypted signing key.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase decrypting the signing key. Secret — never logged or returned.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest. Used verbatim.</summary>
    [Required(AllowEmptyStrings = false)]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the fund that set-aside change is invested into.</summary>
    [Required(AllowEmptyStrings = false)]
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this app's PublicApi host can be reached by Upvest webhooks.</summary>
    [Required(AllowEmptyStrings = false)]
    public string CallbackBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Space-delimited OAuth scopes requested for the access token. This is our own configuration
    /// (not a provider credential); a broad default covers the capabilities the integration uses.
    /// </summary>
    public string Scope { get; set; } =
        "users:read users:write accounts:read accounts:write " +
        "account_groups:read account_groups:write " +
        "orders:read orders:write webhooks:read webhooks:write";

    /// <summary>
    /// Space-separated RFC 9421 covered components for the outbound HTTP Message Signature.
    /// Tunable without a code change so the exact scheme the provider verifies can be configured.
    /// </summary>
    public string SignatureComponents { get; set; } = "@method @path @authority";

    /// <summary>
    /// Signature algorithm label placed in <c>signature-input</c>. Empty = derive from the key's curve.
    /// </summary>
    public string SignatureAlg { get; set; } = string.Empty;

    /// <summary>ECDSA signature encoding: <c>raw</c> (RFC 9421 r‖s concatenation) or <c>der</c>.</summary>
    public string SignatureFormat { get; set; } = "raw";
}
