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
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Servers;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    private const string HttpClientName = "Upvest";

    /// <summary>
    /// Wires the Upvest integration: binds and fail-fast-validates the <c>Upvest:</c> credentials, builds a
    /// single long-lived signed <see cref="UpvestInvestmentApiClient"/>, and registers the portfolio services.
    /// The host refuses to start if any credential part is missing/blank or the signing key file is unreadable,
    /// rather than discovering it as an authentication failure on the first call.
    /// </summary>
    public static IServiceCollection AddUpvestPortfolio(this IServiceCollection services, IConfiguration configuration)
    {
        // Fail-fast: every credential part must be present and non-blank before the host serves anything —
        // not discovered as an auth failure on the first call. Names only, never values, appear in the messages.
        // The signing key file itself is opened and validated when the signed client is constructed (below).
        services.AddOptions<UpvestOptions>()
            .Bind(configuration.GetSection(UpvestOptions.ConfigSection))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "Upvest:ClientId is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), "Upvest:ClientSecret is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKeyId), "Upvest:SigningKeyId is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKeyPath), "Upvest:SigningKeyPath is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Upvest:BaseUrl is not configured.")
            .ValidateOnStart();

        // One named HttpClient: bounds each attempt and recycles pooled connections behind the long-lived client.
        services.AddHttpClient(HttpClientName, c =>
            {
                c.Timeout = TimeSpan.FromSeconds(30); // per-attempt backstop
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<UpvestOptions>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var errorLogger = loggerFactory.CreateLogger("Upvest.ProviderErrors");

            if (!File.Exists(options.SigningKeyPath))
            {
                throw new InvalidOperationException(
                    "Upvest:SigningKeyPath does not point to a readable signing key file.");
            }

            // Request signing is required on every Upvest call, including the token request.
            var signingCredentials = UpvestHttpSignatureCredentials.FromFile(
                keyId: options.SigningKeyId,
                privateKeyPath: options.SigningKeyPath,
                upvestClientId: options.ClientId,
                privateKeyPassphrase: string.IsNullOrEmpty(options.SigningKeyPassphrase) ? null : options.SigningKeyPassphrase);

            var clientOptions = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = options.ClientId,
                    ClientSecret = options.ClientSecret,
                },
                Hooks = new UpvestInvestmentApi.Core.Hooks.SdkHook[]
                {
                    new UpvestSigningHook(signingCredentials),
                    // Observability: on an error response, record Upvest's correlation id (request_id) and title.
                    // PII-safe — the free-text `detail` (which can echo submitted values) is deliberately not logged.
                    UpvestInvestmentApi.Core.Hooks.SdkHook.OnResponse((res, ctx) =>
                        UpvestProviderErrorLog.Record(errorLogger, res)),
                },
                // Bound a GET's worst case; writes (POST) are not retried by default.
                Retry = RetryOptions.Default() with
                {
                    Timeout = TimeSpan.FromSeconds(20),
                    MaxRetries = 2,
                },
                // Sensitive onboarding/tax/payment data flows through these requests: assign the logger factory
                // explicitly (disarms the UPVEST_INVESTMENT_API_LOG=trace body-logging footgun) and never log bodies.
                Logging = new LoggingOptions
                {
                    LoggerFactory = loggerFactory,
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false,
                },
            };

            // Upvest:BaseUrl is the base address for every call, used verbatim.
            clientOptions.Server.Default.Production.BaseUrl = options.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, clientOptions);
        });

        services.AddSingleton<IUpvestPortfolioService, UpvestPortfolioService>();
        services.AddScoped<IPortfolioService, PortfolioService>();

        return services;
    }
}
