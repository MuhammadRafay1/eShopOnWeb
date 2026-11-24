namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the "Upvest" configuration section. Values are
/// supplied via user-secrets / environment and must never be written into the repository.
/// </summary>
public class UpvestSettings
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyPath { get; set; } = string.Empty;
    public string SigningKeyPassphrase { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}
