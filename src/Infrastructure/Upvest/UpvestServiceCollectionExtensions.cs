using System;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.eShopWeb.ApplicationCore.Services.Investing;
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
    /// <summary>The named HttpClient scoped to Upvest, so the signing handler and timeout do not leak to other clients.</summary>
    public const string HttpClientName = "Upvest";

    /// <summary>
    /// Registers the Upvest investing integration: the single signing/auth DelegatingHandler, the SDK client
    /// (base URL bound from <c>Upvest:BaseUrl</c>, no credentials on the client — the handler supplies auth),
    /// the provider gateway, and the investing domain services.
    /// </summary>
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestOptions>(configuration.GetSection(UpvestOptions.SectionName));

        services.AddSingleton<IUpvestRequestSigner, UpvestRequestSigner>();
        services.AddSingleton<IUpvestTokenProvider, UpvestTokenProvider>();
        services.AddTransient<UpvestAuthenticationHandler>();

        services.AddHttpClient(HttpClientName, client =>
            {
                // Per-attempt backstop; the SDK's RetryOptions.Timeout bounds each attempt too.
                client.Timeout = TimeSpan.FromSeconds(25);
            })
            .AddHttpMessageHandler<UpvestAuthenticationHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5), // keeps DNS fresh behind the singleton client
            });

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<UpvestOptions>>().Value;
            var options = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                // Per-attempt timeout; a CancellationToken bounds the whole call where callers need it.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(20) },
                // Explicit LoggerFactory + bodies off: personal data in request bodies must never be logged,
                // and this prevents the SDK log environment variable from switching body logging on.
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false,
                },
            };
            // Every call targets exactly the configured base URL, never the live Upvest system.
            options.Server.Default.Production.BaseUrl = opts.BaseUrl;

            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddSingleton<IShopperConcurrencyGuard, ShopperConcurrencyGuard>();
        services.AddScoped<IUpvestInvestorGateway, UpvestInvestorGateway>();
        services.AddScoped<IInvestingService, InvestingService>();
        services.AddScoped<IInvestingProcessor, InvestingProcessor>();

        return services;
    }
}
