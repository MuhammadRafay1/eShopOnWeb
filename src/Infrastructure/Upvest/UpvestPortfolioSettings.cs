namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Strongly-typed Upvest configuration, bound from the <c>Upvest:</c> section. The secret members
/// (<see cref="ClientSecret"/>, <see cref="SigningKeyPassphrase"/>) are never logged or returned by an endpoint.
/// </summary>
public class UpvestPortfolioSettings
{
    public const string SectionName = "Upvest";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? SigningKeyId { get; set; }
    public string? SigningKeyPath { get; set; }
    public string? SigningKeyPassphrase { get; set; }
    public string? BaseUrl { get; set; }
}
