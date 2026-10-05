using System;
using System.Net.Http;
using MaxioAdvancedBilling;
using MaxioAdvancedBilling.Core.Authentication.Basic;
using MaxioAdvancedBilling.Core.Configuration;
using MaxioAdvancedBilling.Servers;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Billing;

public static class MaxioServiceCollectionExtensions
{
    /// <summary>Name of the <see cref="HttpClient"/> dedicated to Maxio (tests replace its primary handler).</summary>
    public const string HttpClientName = "Maxio";

    public static IServiceCollection AddMaxioSubscriptionBilling(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MaxioSettings>()
            .Bind(configuration.GetSection(MaxioSettings.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<MaxioSettings>, MaxioSettingsValidator>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient(HttpClientName, (sp, client) =>
            {
                // Per-attempt backstop, slightly above the SDK's own per-attempt timeout so that one fires first and
                // a GET is retried. The request-wide budget is enforced by MaxioBillingGateway.
                var settings = sp.GetRequiredService<IOptions<MaxioSettings>>().Value;
                client.Timeout = settings.AttemptTimeout + TimeSpan.FromSeconds(2);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // The SDK client is a singleton holding one HttpClient; recycle connections so DNS changes are seen.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MaxioSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            return new MaxioAdvancedBillingClient(httpClient, BuildClientOptions(settings,
                sp.GetRequiredService<ILoggerFactory>(), sp.GetRequiredService<TimeProvider>()));
        });

        services.AddScoped<ISubscriptionBillingGateway, MaxioBillingGateway>();
        services.AddScoped<ISubscriptionClaimStore, SubscriptionClaimStore>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        return services;
    }

    /// <summary>
    /// Options are built once and captured by the singleton client: a rotated API key takes effect on restart.
    /// </summary>
    public static MaxioAdvancedBillingClientOptions BuildClientOptions(MaxioSettings settings, ILoggerFactory loggerFactory,
        TimeProvider timeProvider)
    {
        var options = new MaxioAdvancedBillingClientOptions
        {
            Environment = settings.IsEu ? ServerEnvironment.Eu : ServerEnvironment.Us,
            BasicAuth = new BasicAuthCredentials { Username = settings.ApiKey!, Password = "x" },
            // Default retry verbs (GET/HEAD/PUT/OPTIONS) are kept: the POSTs this integration makes are never resent.
            Retry = RetryOptions.Default() with { Timeout = settings.AttemptTimeout },
            // Assigned explicitly so the SDK's log environment variable cannot switch body logging on: customer
            // create bodies carry names and e-mail addresses.
            Logging = new LoggingOptions
            {
                LoggerFactory = loggerFactory,
                LogRequestBody = false,
                LogRequestHeaders = false,
                LogResponseHeaders = false,
            },
            TimeProvider = timeProvider,
        };

        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? null : settings.BaseUrl.Trim().TrimEnd('/');
        if (settings.IsEu)
        {
            if (!string.IsNullOrWhiteSpace(settings.Subdomain)) options.Server.Production.Eu.Site = settings.Subdomain.Trim();
            if (baseUrl is not null) options.Server.Production.Eu.BaseUrl = baseUrl;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(settings.Subdomain)) options.Server.Production.Us.Site = settings.Subdomain.Trim();
            if (baseUrl is not null) options.Server.Production.Us.BaseUrl = baseUrl;
        }

        return options;
    }
}
