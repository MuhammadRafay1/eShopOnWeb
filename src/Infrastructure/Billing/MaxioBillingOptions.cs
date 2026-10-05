using System;

namespace Microsoft.eShopWeb.Infrastructure.Billing;

/// <summary>
/// Configuration bound from the "Maxio" configuration section. Values are
/// supplied via user secrets / environment variables — never committed.
/// </summary>
public sealed class MaxioBillingOptions
{
    public const string SectionName = "Maxio";

    /// <summary>Maxio Billing API key, used as the Basic-auth username.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Subdomain of the Maxio site (e.g. the sandbox site).</summary>
    public string? Subdomain { get; set; }

    /// <summary>Handle of the product family that contains the subscription plans.</summary>
    public string? ProductFamilyHandle { get; set; }

    /// <summary>
    /// Optional override for the API base address. When set, it is used
    /// verbatim instead of deriving the address from <see cref="Subdomain"/>.
    /// </summary>
    public string? BaseUrl { get; set; }

    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            return BaseUrl.Trim().TrimEnd('/');
        }

        if (string.IsNullOrWhiteSpace(Subdomain))
        {
            throw new InvalidOperationException(
                "Maxio billing is not configured: set 'Maxio:BaseUrl' or 'Maxio:Subdomain'.");
        }

        return $"https://{Subdomain.Trim()}.chargify.com";
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Maxio billing is not configured: 'Maxio:ApiKey' is missing.");
        }

        if (string.IsNullOrWhiteSpace(BaseUrl) && string.IsNullOrWhiteSpace(Subdomain))
        {
            throw new InvalidOperationException(
                "Maxio billing is not configured: set 'Maxio:BaseUrl' or 'Maxio:Subdomain'.");
        }

        if (string.IsNullOrWhiteSpace(ProductFamilyHandle))
        {
            throw new InvalidOperationException(
                "Maxio billing is not configured: 'Maxio:ProductFamilyHandle' is missing.");
        }
    }
}