namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> configuration section.
/// The client secret and signing-key passphrase are secrets: they are read from configuration
/// (user-secrets / environment) and are never logged or returned by any endpoint.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest. Used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The fund (instrument) that set-aside change is invested in.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this application can be reached by Upvest (for webhooks).</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;

    /// <summary>Scopes requested for the OAuth access token — everything the integration needs.</summary>
    public static readonly string[] Scopes =
    {
        "users:admin", "users:read",
        "checks:admin",
        "accounts:admin", "accounts:read",
        "orders:admin", "orders:read",
        "taxes:admin",
        "virtual_cash_balances:admin",
        "positions:read",
        "webhooks:admin", "webhooks:read"
    };
}
