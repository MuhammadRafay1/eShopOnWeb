using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Servers;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

public static class UpvestServiceCollectionExtensions
{
    private const string HttpClientName = "Upvest";

    /// <summary>
    /// Registers the Upvest SDK client and the single authentication handler that owns all credentials.
    /// Options are validated on start (fail-fast). The handler attaches the bearer token and HTTP Message
    /// Signature to every call; no call site attaches credentials.
    /// </summary>
    public static IServiceCollection AddUpvestClient(this IServiceCollection services, IConfiguration configuration)
    {
        // Required-field and signing-key checks live in UpvestOptionsValidator (registered below);
        // ValidateOnStart() forces them to run at startup so a misconfiguration fails fast.
        services.AddSingleton<IValidateOptions<UpvestOptions>, UpvestOptionsValidator>();

        services.AddOptions<UpvestOptions>()
            .Bind(configuration.GetSection(UpvestOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton(sp =>
            new UpvestRequestSigner(sp.GetRequiredService<IOptions<UpvestOptions>>().Value));

        services.AddSingleton<IUpvestTokenProvider, UpvestTokenProvider>();
        services.AddTransient<UpvestAuthenticationHandler>();

        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .AddHttpMessageHandler<UpvestAuthenticationHandler>()
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
                // Explicit logger so the UPVESTINVESTMENTAPICLIENT_LOG env var cannot force body logging on;
                // request bodies carry PII and are never logged.
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogResponseHeaders = false,
                    LogRequestHeaders = false,
                },
                // OauthClientCredentials deliberately left unset: the single handler owns authentication,
                // so the per-operation OAuth scheme resolves to a no-op (NoneAuthScheme).
            };
            clientOptions.Server.Default.Production.BaseUrl = options.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, clientOptions);
        });

        return services;
    }

    /// <summary>
    /// Registers the full "invest your change" capability: the Upvest client/auth stack, the gateway, the
    /// orchestration and order-placement services, the reconciler and its hosted timer.
    /// </summary>
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddUpvestClient(configuration);

        services.AddScoped<IUpvestInvestorGateway, UpvestInvestorGateway>();
        services.AddScoped<IInvestingService, InvestingService>();
        services.AddScoped<IOrderPlacementService, OrderPlacementService>();
        services.AddScoped<IInvestmentReconciler, InvestmentReconciler>();
        services.AddHostedService<SettlementReconciler>();

        return services;
    }
}
