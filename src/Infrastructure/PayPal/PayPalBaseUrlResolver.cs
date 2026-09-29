using System;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Resolves the PayPal API base URL. If <see cref="PayPalOptions.BaseUrl"/> is set it is used
/// verbatim for every call (including the token request); otherwise it is derived from the
/// environment. The sandbox host is the literal value declared in every spec's servers[0].url.
/// </summary>
public static class PayPalBaseUrlResolver
{
    public const string SandboxBaseUrl = "https://api-m.sandbox.paypal.com";
    public const string LiveBaseUrl = "https://api-m.paypal.com";

    public static string Resolve(PayPalOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return options.BaseUrl!.TrimEnd('/');
        }

        return options.Environment?.Trim().ToLowerInvariant() switch
        {
            "live" or "production" => LiveBaseUrl,
            _ => SandboxBaseUrl
        };
    }

    public static Uri ResolveUri(PayPalOptions options) => new(Resolve(options));
}
