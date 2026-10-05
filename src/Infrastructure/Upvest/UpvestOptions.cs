using System.ComponentModel.DataAnnotations;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Upvest connection settings, bound from the <c>Upvest</c> configuration section. Every value is supplied
/// by configuration (user-secrets / environment) — none is hard-coded. The two secrets (ClientSecret,
/// SigningKeyPassphrase) are never logged or returned by an endpoint.
/// </summary>
public sealed class UpvestOptions
{
    public const string SectionName = "Upvest";

    [Required] public string ClientId { get; set; } = string.Empty;
    [Required] public string ClientSecret { get; set; } = string.Empty;
    [Required] public string SigningKeyId { get; set; } = string.Empty;
    [Required] public string SigningKeyPath { get; set; } = string.Empty;
    [Required] public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>Base address for EVERY call to Upvest; used verbatim.</summary>
    [Required] public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The fund (ISIN) the change is invested in.</summary>
    [Required] public string InstrumentId { get; set; } = string.Empty;

    /// <summary>Public base address at which this application's PublicApi host is reachable from Upvest.</summary>
    [Required] public string CallbackBaseUrl { get; set; } = string.Empty;

    /// <summary>OAuth scopes requested for the access token. Not a secret.</summary>
    public string Scopes { get; set; } =
        "users:admin checks:admin taxes:admin accounts:admin accounts:read orders:admin orders:read " +
        "instruments:read payments:admin payments:read webhooks:admin webhooks:read positions:read";

    /// <summary>Relative path (on the PublicApi host) that receives Upvest webhook deliveries.</summary>
    public string WebhookPath { get; set; } = "/api/investing/upvest/webhooks";

    public string WebhookUrl => $"{CallbackBaseUrl.TrimEnd('/')}{WebhookPath}";
}
