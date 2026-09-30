using System;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayPalServerSdk;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Servers;

namespace Microsoft.eShopWeb.Infrastructure;

/// <summary>
/// Registers the PayPal integration. Called only from the PublicApi host — the Web storefront never
/// talks to PayPal, so it does not pay for this registration.
/// </summary>
public static class PayPalDependencies
{
    // A named HttpClient keeps this SDK's pipeline (timeout, pooled-handler lifetime) off the shared
    // default client, and lets the singleton SDK client rotate DNS via PooledConnectionLifetime.
    public const string HttpClientName = "PayPalServerSdk";

    public static void ConfigurePayPalServices(IConfiguration configuration, IServiceCollection services)
    {
        services.Configure<PayPalOptions>(configuration.GetSection(PayPalOptions.ConfigSectionName));

        services.AddHttpClient(HttpClientName, c =>
            {
                // Bounds a single attempt (not the whole call); a hung provider ends here, not after ~100s.
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // The SDK client is a long-lived singleton, so refresh DNS behind it.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);

            var clientOptions = new PayPalServerSdkClientOptions
            {
                // Sandbox is the only ServerEnvironment this SDK exposes; a non-sandbox host is
                // reached only via the BaseUrl override below (see PayPalOptions.BaseUrl).
                Environment = ServerEnvironment.Sandbox,
                Oauth2 = new OAuth2ClientCredentials
                {
                    ClientId = options.ClientId,
                    ClientSecret = options.ClientSecret
                }
                // Leave Oauth2TokenStrategy null so the token request honours the BaseUrl override too.
            };

            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                // Used verbatim for every call INCLUDING the OAuth2 token request.
                clientOptions.Server.Default.Sandbox.BaseUrl = options.BaseUrl;
            }

            return new PayPalServerSdkClient(httpClient, clientOptions);
        });

        services.AddScoped<IPaymentGatewayService, PayPalPaymentGatewayService>();
    }
}
