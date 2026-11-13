using System;
using System.IO;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
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
    /// Registers the Upvest client (as a long-lived singleton), the portfolio gateway and the
    /// portfolio application service. Binds <see cref="UpvestSettings"/> from the <c>Upvest:</c>
    /// configuration section and fails host startup fast if a credential is missing or the signing
    /// key file is absent, rather than discovering it on the first call in production.
    /// </summary>
    public static IServiceCollection AddUpvestPortfolio(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            // Fail fast at startup on any missing credential — naming the config key, never the value.
            .Validate(s => !string.IsNullOrWhiteSpace(s.ClientId), "Upvest:ClientId is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.ClientSecret), "Upvest:ClientSecret is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SigningKeyId), "Upvest:SigningKeyId is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SigningKeyPath), "Upvest:SigningKeyPath is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.BaseUrl), "Upvest:BaseUrl is not configured.")
            .ValidateOnStart();

        // One named HttpClient with a bounded per-attempt timeout and a recycled connection pool so a
        // long-lived singleton client still picks up DNS changes.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);

            // The signing key file must exist before any call can be signed — fail clearly (naming the
            // config key, never a value) the first time the client is built, not deep inside a request.
            if (!File.Exists(settings.SigningKeyPath))
                throw new InvalidOperationException(
                    "Upvest:SigningKeyPath does not point to an existing signing key file.");

            var signingCredentials = UpvestHttpSignatureCredentials.FromFile(
                keyId: settings.SigningKeyId,
                privateKeyPath: settings.SigningKeyPath,
                upvestClientId: settings.ClientId,
                privateKeyPassphrase: string.IsNullOrEmpty(settings.SigningKeyPassphrase) ? null : settings.SigningKeyPassphrase);

            var options = new UpvestInvestmentApiClientOptions
            {
                // Sandbox environment; BaseUrl is taken verbatim from configuration for every call.
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret
                },
                // HTTP message signing is required by Upvest on every request, including the token call.
                Hooks = new[] { new UpvestSigningHook(signingCredentials) },
                // Bound a single attempt; the overall call budget is the CancellationToken threaded
                // from the request.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) },
                // Logging posture for a flow that carries personal data: an explicit LoggerFactory (so
                // the UPVEST_INVESTMENT_API_LOG environment variable can never force body logging) and
                // request/response bodies and headers never logged.
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false
                },
            };

            options.Server.Default.Production.BaseUrl = settings.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddScoped<IUpvestPortfolioGateway, UpvestPortfolioGateway>();
        services.AddScoped<IPortfolioService, PortfolioService>();

        return services;
    }
}
