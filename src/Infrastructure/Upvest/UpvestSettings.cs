namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> section. All values come from
/// configuration (user-secrets / environment) — none are hard-coded. The client secret and signing-key
/// passphrase are secrets and must never be logged or returned by an endpoint.
/// </summary>
public class UpvestSettings
{
    public const string ConfigSection = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}
