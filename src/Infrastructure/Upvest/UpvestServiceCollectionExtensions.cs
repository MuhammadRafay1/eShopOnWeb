using System;
using System.IO;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
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

/// <summary>
/// Registers the Upvest Investment API client and the shop's <see cref="IUpvestInvestmentService"/>.
/// The client is bound entirely from the <c>Upvest:</c> configuration section (no hard-coded values), with
/// OAuth2 client credentials plus the mandatory HTTP message-signing hook, and the base URL taken from config.
/// The host refuses to start if any credential is missing — a deployment fault is caught at boot, not on the
/// first call. Secrets never reach a log: SDK body logging stays off and the logger factory is set explicitly.
/// </summary>
public static class UpvestServiceCollectionExtensions
{
    private const string HttpClientName = "UpvestInvestmentApi";

    public static IServiceCollection AddUpvestInvestment(this IServiceCollection services, IConfiguration configuration)
    {
        // Fail-fast: bind + validate every credential at startup (each part checked separately), and confirm
        // the signing key file exists and is readable.
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.ClientId), "Upvest:ClientId is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.ClientSecret), "Upvest:ClientSecret is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SigningKeyId), "Upvest:SigningKeyId is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SigningKeyPath), "Upvest:SigningKeyPath is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SigningKeyPassphrase), "Upvest:SigningKeyPassphrase is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.BaseUrl), "Upvest:BaseUrl is not configured.")
            .Validate(s => File.Exists(s.SigningKeyPath),
                "Upvest:SigningKeyPath does not point to an existing signing key file.")
            .ValidateOnStart();

        services.AddHttpClient(HttpClientName, c =>
            {
                // Bounds one attempt; the overall call is bounded by the request CancellationToken the handlers pass.
                c.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // The SDK client is a singleton, so recycle pooled connections to keep DNS fresh.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var options = new UpvestInvestmentApiClientOptions
            {
                // Sandbox environment; base URL taken verbatim from config.
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret
                },
                // Mandatory HTTP message signing on every request (including the token request).
                Hooks = new[]
                {
                    new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(
                        keyId: settings.SigningKeyId,
                        privateKeyPath: settings.SigningKeyPath,
                        upvestClientId: settings.ClientId,
                        privateKeyPassphrase: settings.SigningKeyPassphrase))
                },
                // Idempotent/safe retries only (POST/PUT writes carry deterministic idempotency keys);
                // keep the per-attempt timeout bounded.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(20), MaxRetries = 2 },
                // Set the logger factory explicitly so the UPVEST_INVESTMENT_API_LOG=trace body-logging backdoor
                // cannot be armed, and keep request/response bodies out of logs (sign-up carries personal data).
                Logging = new LoggingOptions
                {
                    LoggerFactory = loggerFactory,
                    LogRequestBody = false,
                    LogResponseHeaders = false,
                    LogRequestHeaders = false
                }
            };

            // Override the base address for every call with the configured value.
            options.Server.Default.Production.BaseUrl = settings.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddScoped<IUpvestInvestmentService, UpvestInvestmentService>();

        return services;
    }
}
