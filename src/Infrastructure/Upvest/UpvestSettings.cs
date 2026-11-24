namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Bound from the <c>Upvest:</c> configuration section. Values come from configuration only
/// (environment variables / user-secrets) and are never hard-coded. The client secret and the
/// signing-key passphrase are secrets and are never logged or returned by any endpoint.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;

    /// <summary>The base address for every call to Upvest; used verbatim.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
