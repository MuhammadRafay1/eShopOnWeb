using System;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> section. Every value is required;
/// the host refuses to start if any is missing or blank (see <c>AddUpvestInvesting</c>). The two secrets
/// (<see cref="ClientSecret"/>, <see cref="SigningKeyPassphrase"/>) are never logged or returned.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    /// <summary>Upvest client id (also sent as the <c>upvest-client-id</c> header). A UUID.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client secret. Secret — never logged.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Key id (<c>keyid</c>) of the request-signing key.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM private key used to sign requests.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase for the signing key. Secret — never logged.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest, used verbatim.</summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the fund the set-aside change is invested in.</summary>
    [Required(AllowEmptyStrings = false)]
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this app's PublicApi host is reachable (for Upvest callbacks).</summary>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string CallbackBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Space-delimited OAuth scopes requested for the access token. Not a secret; override via
    /// <c>Upvest:Scope</c> for your client's entitlements.
    /// </summary>
    public string Scope { get; set; } =
        "users:admin users:read accounts:admin accounts:read orders:admin orders:read instruments:read positions:read checks:admin checks:read taxes:admin taxes:read payments:admin payments:read webhooks:admin webhooks:read";
}
