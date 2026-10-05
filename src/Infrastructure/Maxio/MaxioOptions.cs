namespace Microsoft.eShopWeb.Infrastructure.Maxio;

/// <summary>
/// Configuration for the Maxio Advanced Billing integration, bound from the "Maxio" section.
/// Values are supplied via user secrets / environment (never committed to the repository):
///   Maxio:ApiKey              — the Advanced Billing API key
///   Maxio:Subdomain           — the site subdomain, e.g. "acme" for acme.chargify.com
///   Maxio:ProductFamilyHandle — handle of the product family that holds the subscription plans
///   Maxio:BaseUrl             — optional override; when set it is used verbatim instead of
///                               deriving https://{subdomain}.chargify.com
/// </summary>
public class MaxioOptions
{
    public const string SectionName = "Maxio";

    public string ApiKey { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;

    public string ProductFamilyHandle { get; set; } = string.Empty;

    public string? BaseUrl { get; set; }
}