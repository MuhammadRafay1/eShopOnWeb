namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> section. Values are supplied
/// through configuration (user-secrets / environment); none are hard-coded. The two secret values
/// (<see cref="ClientSecret"/> and <see cref="SigningKeyPassphrase"/>) are never logged or returned.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    /// <summary>OAuth client id issued by Upvest (also sent as the <c>upvest-client-id</c> header).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client secret. Secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Identifier Upvest holds for the registered public signing key (the signature <c>keyid</c>).</summary>
    public string SigningKeyId { get; set; } = string.Empty;

    /// <summary>Path to the PEM-encoded EC (P-521) private signing key.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>Passphrase protecting the signing key. Secret.</summary>
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest, used verbatim (e.g. https://sandbox.upvest.co).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional Upvest API version pin, sent as <c>upvest-api-version</c> when set.
    /// </summary>
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Optional business id some account/account-group operations require. Supplied via configuration
    /// when the client operates on behalf of a business; left unset for plain retail onboarding.
    /// </summary>
    public string? BusinessId { get; set; }
}
