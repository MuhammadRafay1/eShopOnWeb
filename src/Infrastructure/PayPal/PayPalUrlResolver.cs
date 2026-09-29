using System;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Resolves the PayPal API base URL used for <em>every</em> call, including the token request.
/// An explicit <c>PayPal:BaseUrl</c> override wins verbatim; otherwise the URL is derived from the
/// configured environment (defaulting to sandbox).
/// </summary>
public static class PayPalUrlResolver
{
    public const string SandboxBaseUrl = "https://api-m.sandbox.paypal.com";
    public const string LiveBaseUrl = "https://api-m.paypal.com";

    public static string Resolve(PayPalOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            return options.BaseUrl.Trim();

        var env = options.Environment?.Trim();
        var isLive = string.Equals(env, "Live", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase);
        return isLive ? LiveBaseUrl : SandboxBaseUrl;
    }
}
