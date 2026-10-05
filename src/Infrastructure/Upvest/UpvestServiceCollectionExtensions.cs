using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Investing;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Servers;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>Registers the "Invest your change" services, including the Upvest SDK client and its auth pipeline.</summary>
public static class UpvestServiceCollectionExtensions
{
    public const string UpvestClientName = "Upvest";

    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        // Fail-fast on missing/blank config: the host refuses to start rather than 401 on the first call.
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);

        // Auth building blocks.
        services.AddSingleton<IUpvestRequestSigner, UpvestRequestSigner>();
        services.AddSingleton<UpvestResponseCapture>();
        services.AddTransient<UpvestAuthDelegatingHandler>();

        // One reusable HttpClient pipeline; every Upvest call flows through the signing handler.
        services.AddHttpClient(UpvestClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .AddHttpMessageHandler<UpvestAuthDelegatingHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        // One long-lived SDK client (options captured once, base URL from config verbatim). OAuth credentials
        // are intentionally not set here — the delegating handler supplies the bearer and the signature.
        services.AddSingleton(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(UpvestClientName);
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var options = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                Logging = new LoggingOptions
                {
                    LoggerFactory = loggerFactory, // set explicitly so the LOG env var cannot arm body logging
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false,
                },
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(10) },
            };
            options.Server.Default.Production.BaseUrl = settings.BaseUrl;
            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddSingleton<IUpvestTokenProvider, UpvestTokenProvider>();
        services.AddScoped<IUpvestInvestingGateway, UpvestInvestingGateway>();

        // Application wiring.
        services.AddSingleton<IInvestingLocks, InvestingLocks>();
        services.AddScoped<IInvestingService, InvestingService>();

        return services;
    }
}
