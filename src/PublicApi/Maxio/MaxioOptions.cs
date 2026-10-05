namespace Microsoft.eShopWeb.PublicApi.Maxio;

/// <summary>
/// Configuration bound from the "Maxio" configuration section.
/// Values are supplied via user-secrets / environment variables - never committed to the repo.
/// </summary>
public class MaxioOptions
{
    public const string SectionName = "Maxio";

    /// <summary>
    /// The Maxio Advanced Billing API key (Basic auth username; the password is "x" per the spec's security scheme).
    /// Source: MAXIO_API_KEY.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The site subdomain used to build the base URL (https://{subdomain}.chargify.com per the spec's server templating).
    /// Source: MAXIO_SITE_SUBDOMAIN.
    /// </summary>
    public string Subdomain { get; set; } = string.Empty;

    /// <summary>
    /// The handle of the product family that contains the subscription plans exposed by this app.
    /// Source: MAXIO_DEFAULT_PRODUCT_FAMILY.
    /// </summary>
    public string ProductFamilyHandle { get; set; } = string.Empty;

    /// <summary>
    /// Optional verbatim override for the API base address. When null/empty the base address is
    /// derived from <see cref="Subdomain"/> using the spec's default (US) server URL template.
    /// </summary>
    public string? BaseUrl { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        (!string.IsNullOrWhiteSpace(BaseUrl) || !string.IsNullOrWhiteSpace(Subdomain));

    public string GetBaseAddress()
    {
        if (!IsConfigured)
        {
            throw new MaxioConfigurationException(
                "Maxio billing is not configured. Provide Maxio:ApiKey and Maxio:Subdomain " +
                "(or Maxio:BaseUrl) via user-secrets or environment variables.");
        }

        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl.TrimEnd('/');
        }

        // Spec server templating: US environment -> https://{site}.chargify.com
        return $"https://{Subdomain.Trim()}.chargify.com";
    }
}