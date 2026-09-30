using System;
using Microsoft.eShopWeb.ApplicationCore.Configuration;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Resolves the PayPal API base address. PayPal:BaseUrl, when set, overrides everything and is
/// used verbatim for every call including the OAuth token request. Otherwise the address is
/// derived from PayPal:Environment, matching the "server" declared in the api-specs documents.
/// </summary>
public static class PayPalBaseUrlResolver
{
    public const string SANDBOX_URL = "https://api-m.sandbox.paypal.com";
    public const string LIVE_URL = "https://api-m.paypal.com";

    public static string Resolve(PayPalSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            return settings.BaseUrl!;
        }

        return settings.Environment?.Trim().ToLowerInvariant() switch
        {
            "sandbox" => SANDBOX_URL,
            "live" => LIVE_URL,
            "production" => LIVE_URL,
            _ => throw new InvalidOperationException(
                $"Unknown PayPal:Environment '{settings.Environment}'. Expected 'sandbox' or 'live' (or set PayPal:BaseUrl explicitly).")
        };
    }
}
