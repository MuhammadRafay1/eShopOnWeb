using System;
using System.Net.Http;
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.Authentication.Basic;
using MaxioAdvancedBilling.Core.Configuration;
using MaxioAdvancedBilling.Servers;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Billing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.MaxioBilling;

public static class MaxioBillingServiceCollectionExtensions
{
    /// <summary>The named HttpClient owned by this integration — keeps its handler pipeline off the shared default client.</summary>
    public const string HttpClientName = "MaxioAdvancedBilling";

    /// <summary>Per-attempt timeout for every Maxio call (the SDK's own retry ladder may multiply this on retryable verbs).</summary>
    private static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddMaxioBilling(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MaxioBillingOptions>()
            .Bind(configuration.GetSection(MaxioBillingOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<MaxioBillingOptions>, ValidateMaxioBillingOptions>();

        services.AddHttpClient(HttpClientName, c => c.Timeout = PerAttemptTimeout)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // The SDK client is a singleton, so the pool must recycle connections on its own.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        // The client, its options and its credentials are built ONCE and captured for the process
        // lifetime — a rotated API key takes effect only after a restart.
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MaxioBillingOptions>>().Value;
            var clientOptions = new MaxioAdvancedBillingClientOptions
            {
                Environment = ServerEnvironment.Us,
                BasicAuth = new BasicAuthCredentials
                {
                    Username = settings.ApiKey,
                    Password = "x"
                },
                Retry = RetryOptions.Default() with { Timeout = PerAttemptTimeout },
                // LoggerFactory is assigned explicitly so the MAXIOADVANCEDBILLINGCLIENT_LOG
                // environment variable cannot switch logging (including unredacted request
                // bodies) on from outside the code. LogRequestBody stays off (default) —
                // request bodies carry customer PII.
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>()
                }
            };
            if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                clientOptions.Server.Production.Us.BaseUrl = settings.BaseUrl;
            }
            else
            {
                clientOptions.Server.Production.Us.Site = settings.Subdomain;
            }
            return new MaxioAdvancedBillingClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
                clientOptions);
        });

        services.AddScoped<ISubscriptionBillingService, MaxioSubscriptionBillingService>();

        return services;
    }
}