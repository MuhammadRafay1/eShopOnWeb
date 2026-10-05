namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Everything needed to talk to Upvest, bound from the <c>Upvest:</c> configuration section.
/// Values are supplied through configuration / user-secrets; none are hard-coded.
/// </summary>
public class UpvestOptions
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for every call to Upvest. Used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>ISIN of the fund that set-aside change is invested in.</summary>
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this host's PublicApi can be reached by Upvest.</summary>
    public string CallbackBaseUrl { get; set; } = string.Empty;

    /// <summary>Space-delimited OAuth scopes requested for the access token.</summary>
    public string Scopes { get; set; } =
        "users:admin users:read checks:admin checks:read accounts:admin accounts:read " +
        "orders:admin orders:read virtual_cash_balances:admin webhooks:admin webhooks:read taxes:admin";
}
