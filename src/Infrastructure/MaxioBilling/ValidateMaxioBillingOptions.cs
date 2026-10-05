using System;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.MaxioBilling;

/// <summary>
/// Fail-fast validation for the Maxio billing configuration: the host refuses to start when any
/// required part is missing or blank, rather than surfacing the fault as a 401 on the first call.
/// </summary>
public class ValidateMaxioBillingOptions : IValidateOptions<MaxioBillingOptions>
{
    public ValidateOptionsResult Validate(string? name, MaxioBillingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail(
                $"{MaxioBillingOptions.SectionName}:ApiKey is not configured. Set it via the MAXIO_API_KEY environment variable, user-secrets, or your secret store before starting the app.");
        }
        if (string.IsNullOrWhiteSpace(options.Subdomain))
        {
            return ValidateOptionsResult.Fail(
                $"{MaxioBillingOptions.SectionName}:Subdomain is not configured. Set it via the MAXIO_SITE_SUBDOMAIN environment variable before starting the app.");
        }
        if (string.IsNullOrWhiteSpace(options.ProductFamilyHandle))
        {
            return ValidateOptionsResult.Fail(
                $"{MaxioBillingOptions.SectionName}:ProductFamilyHandle is not configured. Set it via the MAXIO_DEFAULT_PRODUCT_FAMILY environment variable before starting the app.");
        }
        if (!string.IsNullOrWhiteSpace(options.BaseUrl) &&
            !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail(
                $"{MaxioBillingOptions.SectionName}:BaseUrl is configured but is not an absolute URI: it must be a complete base address when set.");
        }
        return ValidateOptionsResult.Success;
    }
}