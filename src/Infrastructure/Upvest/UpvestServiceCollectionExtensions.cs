using System;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Investing;
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
    /// Registers the "invest your change" integration: the Upvest SDK client over a dedicated HttpClient
    /// whose single <see cref="UpvestSigningHandler"/> authenticates every call, plus the gateway, the
    /// application service and the reconciliation worker. Fails fast at startup if any Upvest setting is missing.
    /// </summary>
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(UpvestOptions.SectionName);

        // Fail fast: validate configuration at registration (every credential part checked), so the host
        // refuses to start rather than surfacing a 401 on the first call.
        var validated = section.Get<UpvestOptions>() ?? new UpvestOptions();
        validated.Validate();

        services.Configure<UpvestOptions>(section);

        services.AddSingleton<IUpvestRequestSigner, UpvestRequestSigner>();
        services.AddSingleton<UpvestResponseCapture>();
        services.AddSingleton<IUpvestTokenProvider, UpvestTokenProvider>();
        services.AddTransient<UpvestSigningHandler>();
        services.AddSingleton<InvestingCoordinator>();

        // Dedicated HttpClient: the signing handler sits on this pipeline only, and a bounded timeout plus
        // pooled-connection recycling keep a hung provider or a stale DNS entry from holding the app.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(20))
            .AddHttpMessageHandler<UpvestSigningHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<UpvestOptions>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);

            var clientOptions = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                // Per-attempt timeout; the whole-call budget is enforced by the gateway's CancellationToken.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) },
                // Explicit logger factory so the SDK's log environment variable cannot turn on
                // unredacted request-body logging (the bodies carry personal data).
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false,
                },
            };
            // Use the configured base URL verbatim for every Upvest call.
            clientOptions.Server.Default.Production.BaseUrl = options.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, clientOptions);
        });

        services.AddSingleton<IUpvestGateway, UpvestGateway>();
        services.AddScoped<IInvestingService, InvestingService>();
        services.AddHostedService<UpvestReconciliationService>();

        return services;
    }
}
