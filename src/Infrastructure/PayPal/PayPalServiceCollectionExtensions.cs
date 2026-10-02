using System;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PayPalServerSdk;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Core.Configuration;
using PayPalServerSdk.Servers;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Wires the PayPal integration: binds settings, fails fast on missing credentials, registers the SDK
/// client over a named, long-lived HttpClient, and registers the payment services.
/// </summary>
public static class PayPalServiceCollectionExtensions
{
    private const string HttpClientName = "PayPalServerSdk";

    public static IServiceCollection AddPayPalIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new PayPalSettings();
        configuration.GetSection(PayPalSettings.CONFIG_NAME).Bind(settings);
        ValidateFailFast(settings);

        services.AddSingleton(settings);

        // One long-lived HttpClient (pooled-connection recycling keeps DNS fresh behind a singleton client),
        // bounded per attempt; the whole-call budget is enforced in the gateway via a CancellationToken.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(40))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var options = new PayPalServerSdkClientOptions
            {
                Environment = ServerEnvironment.Sandbox,
                Oauth2 = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId!.Trim(),
                    ClientSecret = settings.ClientSecret!.Trim()
                },
                // Explicit logger factory disables the PAYPALSERVERSDKCLIENT_LOG env var; request bodies
                // (which carry card data) are never logged.
                Logging = new LoggingOptions
                {
                    LoggerFactory = loggerFactory,
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false
                },
                // Per-attempt timeout; POST/DELETE are never auto-retried by the SDK.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(30) }
            };

            // When a base URL is configured, it applies to every call — including the OAuth token request,
            // which the SDK derives from this same server configuration.
            if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                options.Server.Default.Sandbox.BaseUrl = settings.BaseUrl!.Trim();
            }

            return new PayPalServerSdkClient(httpClient, options);
        });

        services.AddScoped<IPaymentGateway, PayPalPaymentGateway>();
        services.AddSingleton<IPaymentLock, PaymentLock>();
        services.AddScoped<IOrderPaymentService, OrderPaymentService>();
        services.AddScoped<ISavedCardService, SavedCardService>();
        services.AddScoped<IReconciliationService, ReconciliationService>();

        return services;
    }

    private static void ValidateFailFast(PayPalSettings settings)
    {
        RequireValue(settings.ClientId, $"{PayPalSettings.CONFIG_NAME}:ClientId");
        RequireValue(settings.ClientSecret, $"{PayPalSettings.CONFIG_NAME}:ClientSecret");
        RequireValue(settings.Environment, $"{PayPalSettings.CONFIG_NAME}:Environment");
        RequireValue(settings.Currency, $"{PayPalSettings.CONFIG_NAME}:Currency");

        var isSandbox = string.Equals(settings.Environment!.Trim(), "sandbox", StringComparison.OrdinalIgnoreCase);
        if (!isSandbox && string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            // The SDK only declares a Sandbox environment; any other environment must be reached via BaseUrl,
            // so we never silently send test traffic to a live host (or vice versa).
            throw new InvalidOperationException(
                $"{PayPalSettings.CONFIG_NAME}:Environment is '{settings.Environment}', which is not 'sandbox'. " +
                $"Set {PayPalSettings.CONFIG_NAME}:BaseUrl to the API base address for that environment.");
        }
    }

    private static void RequireValue(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} is not configured. Set it via user-secrets or an environment variable before starting the app.");
        }
    }
}
