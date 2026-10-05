namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Settings bound from the <c>Upvest:</c> configuration section. Values come from .NET user-secrets /
/// environment variables and are never written into the repository. <see cref="ClientSecret"/> and
/// <see cref="SigningKeyPassphrase"/> are secrets: never logged, never returned by an endpoint.
/// </summary>
public sealed class UpvestOptions
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest; used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The ISIN of the fund the spare change is invested in.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this application can be reached from outside (reserved for webhooks).</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;

    /// <summary>The OAuth scopes requested for the access token.</summary>
    public string Scopes { get; set; } =
        "users:admin checks:admin taxes:admin accounts:admin orders:admin orders:read instruments:read payments:admin payments:read positions:read";
}
