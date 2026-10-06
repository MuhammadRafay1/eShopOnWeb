using System;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Servers;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    /// <summary>Name of the named HttpClient whose pipeline carries the single authentication handler.</summary>
    public const string HttpClientName = "UpvestInvestmentApi";

    /// <summary>
    /// Registers the Upvest integration: fail-fast settings binding, the single authentication handler
    /// (bearer + HTTP signing), the provider SDK client pointed at the configured base URL, and the
    /// application services for investing spare change.
    /// </summary>
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services)
    {
        services.AddOptions<UpvestSettings>()
            .BindConfiguration(UpvestSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Signing key and token cache are process-wide singletons.
        services.AddSingleton<UpvestRequestSigner>();
        services.AddSingleton<UpvestAccessTokenProvider>();

        // The one reusable handler every Upvest call passes through.
        services.AddTransient<UpvestAuthenticationHandler>();

        services.AddHttpClient(HttpClientName, client =>
            {
                // Bounds one attempt; the whole call is bounded by the caller's CancellationToken.
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<UpvestAuthenticationHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpvestSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);

            var options = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                // The SDK never attaches OAuth itself — the handler owns authentication.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(30) },
            };
            // Use the configured base URL verbatim instead of the SDK default sandbox host.
            options.Server.Default.Production.BaseUrl = settings.BaseUrl;
            // Pin the logger so the SDK log environment variable cannot turn on body logging (PII).
            options.Logging = options.Logging with
            {
                LoggerFactory = sp.GetService<ILoggerFactory>(),
                LogRequestBody = false
            };

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddSingleton<IUpvestGateway, UpvestGateway>();
        services.AddScoped<IInvestingService, InvestingService>();

        services.AddHostedService<UpvestStartupValidator>();

        return services;
    }
}
