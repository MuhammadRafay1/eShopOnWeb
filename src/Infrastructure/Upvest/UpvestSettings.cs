namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed binding of the <c>Upvest:</c> configuration section.
/// Values are supplied via configuration / user-secrets and are never hard-coded.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    /// <summary>OAuth2 client id (also the key owner on file at Upvest).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret. Secret – never logged or returned.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Identifier of the public signing key Upvest holds for this client.</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Filesystem path to the PEM-encoded EC (P-521) private signing key.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase protecting the private signing key. Secret – never logged or returned.</summary>
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest (e.g. https://sandbox.upvest.co).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional Upvest business id required by account / account-group creation in some
    /// operating models. Omitted from requests when not configured.
    /// </summary>
    public string? BusinessId { get; set; }

    /// <summary>
    /// Space separated OAuth2 scopes requested for the access token. The token request fails if
    /// it asks for any scope the client is not provisioned for, so this defaults to the single
    /// scope this sandbox grants. Widen it (via <c>Upvest:Scopes</c>) to the full set
    /// (accounts:admin, portfolios:admin, orders:admin, payments:admin, topups:admin,
    /// virtual_cash_balances:admin, positions:read, valuations:read, …) in a fully-provisioned
    /// production environment.
    /// </summary>
    public string Scopes { get; set; } = "users:admin";

    /// <summary>
    /// Consent document id for terms &amp; conditions. Left unset by default because the documented
    /// sandbox fixture is not valid for this client; set it to your client's real consent document
    /// id in production. When empty the consent block is omitted from the create-user request.
    /// </summary>
    public string TermsAndConditionsConsentId { get; set; } = string.Empty;

    /// <summary>
    /// Consent document id for the data-privacy &amp; sharing agreement. Left unset by default for the
    /// same reason as <see cref="TermsAndConditionsConsentId"/>.
    /// </summary>
    public string DataPrivacyConsentId { get; set; } = string.Empty;
}
