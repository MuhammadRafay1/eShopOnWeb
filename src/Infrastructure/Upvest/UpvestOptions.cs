namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Bound from the <c>Upvest:</c> configuration section. Values are supplied via
/// .NET user-secrets (loaded from environment variables); none are hard-coded or
/// written into the repository. <see cref="ClientSecret"/> and
/// <see cref="SigningKeyPassphrase"/> are secrets and are never logged or returned.
/// </summary>
public class UpvestOptions
{
    public const string SectionName = "Upvest";

    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string SigningKeyId { get; set; } = default!;
    public string SigningKeyPath { get; set; } = default!;
    public string SigningKeyPassphrase { get; set; } = default!;
    public string BaseUrl { get; set; } = default!;
    public string InstrumentId { get; set; } = default!;
    public string CallbackBaseUrl { get; set; } = default!;
}
