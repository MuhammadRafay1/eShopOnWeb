using System;
using System.IO;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Servers;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    private const string HttpClientName = "Upvest";

    /// <summary>
    /// Registers the whole "invest your change" capability: fail-fast Upvest configuration, the single
    /// authenticating <see cref="UpvestAuthHandler"/>, the SDK client over its own named HttpClient, the
    /// Upvest gateway and the investing orchestration service.
    /// </summary>
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind and validate Upvest settings; refuse to boot if any required value is missing/blank.
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(s => File.Exists(s.SigningKeyPath),
                "Upvest:SigningKeyPath does not point at an existing file.")
            .ValidateOnStart();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<UpvestSettings>>().Value);

        // The signing key is loaded once (fail-fast if unreadable); the handler holds the token cache.
        services.AddSingleton<UpvestRequestSigner>();
        services.AddTransient<UpvestAuthHandler>();

        // A named HttpClient scoped to this SDK, with the auth handler and a per-attempt timeout backstop.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .AddHttpMessageHandler<UpvestAuthHandler>();

        // One long-lived SDK client (its token cache and pipelines persist for the process).
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<UpvestSettings>();
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);

            var options = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) },
                Logging = new LoggingOptions
                {
                    // Explicit factory so the SDK's log env var cannot switch on unredacted body logging.
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false
                }
            };

            // Use the configured base URL verbatim instead of the environment default.
            options.Server.Default.Production.BaseUrl = settings.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddScoped<IUpvestInvestorGateway, UpvestClient>();
        services.AddScoped<IInvestingService, InvestingService>();

        return services;
    }
}
