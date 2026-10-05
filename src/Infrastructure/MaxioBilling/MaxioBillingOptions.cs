namespace Microsoft.eShopWeb.Infrastructure.MaxioBilling;

/// <summary>
/// Bound from the <c>Maxio</c> configuration section. Values are sourced from
/// environment variables / user-secrets / the secret store of the deployment — never hard-coded.
/// </summary>
public class MaxioBillingOptions
{
    public const string SectionName = "Maxio";

    /// <summary>Bound from Maxio:ApiKey (from the MAXIO_API_KEY environment variable). The Chargify API key sent as the Basic username.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Bound from Maxio:Subdomain (from MAXIO_SITE_SUBDOMAIN). Substituted for {site} in the server base-URL template.</summary>
    public string? Subdomain { get; set; }

    /// <summary>Bound from Maxio:ProductFamilyHandle (from MAXIO_DEFAULT_PRODUCT_FAMILY). The product family that owns the subscription plans.</summary>
    public string? ProductFamilyHandle { get; set; }

    /// <summary>
    /// Optional, bound from Maxio:BaseUrl. When set, used verbatim as the API base address instead of deriving
    /// one from the subdomain.
    /// </summary>
    public string? BaseUrl { get; set; }
}