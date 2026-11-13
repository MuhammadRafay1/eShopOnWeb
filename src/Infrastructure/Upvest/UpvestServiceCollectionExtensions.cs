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
/// Wires the Upvest Investment API client and the managed-portfolio service into the host.
/// The client is a long-lived singleton over a named <see cref="HttpClient"/> so its OAuth token
/// cache and signing hook survive for the process lifetime, and so its pipeline stays off the
/// shared default factory client.
/// </summary>
public static class UpvestServiceCollectionExtensions
{
    private const string HttpClientName = "UpvestInvestmentApi";

    // Bounds: one attempt waits at most this long for response headers; the whole call is bounded
    // by the per-request CancellationToken the service links (see UpvestPortfolioService).
    private static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddUpvestInvestmentIntegration(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Bind the settings for injection.
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        // Fail-fast: the host refuses to start when any required credential is missing or blank,
        // rather than discovering it as an AuthSchemeException on the first call in production. Each
        // part of each multi-part credential is checked, because a blank part is not a missing one.
        var settingsCheck = configuration.GetSection(UpvestSettings.SectionName).Get<UpvestSettings>() ?? new UpvestSettings();
        foreach (var (key, value) in new[]
        {
            ("Upvest:ClientId", settingsCheck.ClientId),
            ("Upvest:ClientSecret", settingsCheck.ClientSecret),
            ("Upvest:SigningKeyId", settingsCheck.SigningKeyId),
            ("Upvest:SigningKeyPath", settingsCheck.SigningKeyPath),
            ("Upvest:BaseUrl", settingsCheck.BaseUrl)
        })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{key} is not configured. Set it via environment variable, user-secrets, or your " +
                    "secret store before starting the app. (Upvest:SigningKeyPassphrase is optional.)");
            }
        }

        services.AddHttpClient(HttpClientName, c =>
            {
                // Per-attempt; set because the SDK default (100s) is far too long for a request path.
                c.Timeout = PerAttemptTimeout;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // Keeps DNS fresh behind the long-lived singleton client.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);

            if (!File.Exists(settings.SigningKeyPath))
            {
                throw new InvalidOperationException(
                    $"Upvest:SigningKeyPath points at a file that does not exist ('{settings.SigningKeyPath}'). " +
                    "Set it to the path of the Upvest HTTP-signing private key before starting the app.");
            }

            // Both OAuth2 client credentials AND an HTTP message signature are required on every call.
            var signingCredentials = UpvestHttpSignatureCredentials.FromFile(
                keyId: settings.SigningKeyId,
                privateKeyPath: settings.SigningKeyPath,
                upvestClientId: settings.ClientId,
                privateKeyPassphrase: string.IsNullOrEmpty(settings.SigningKeyPassphrase) ? null : settings.SigningKeyPassphrase);

            var options = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret
                },
                Hooks = [new UpvestSigningHook(signingCredentials)],
                // BaseUrl is used verbatim for every call; Environment stays Production, so override
                // the Production base url (the selected environment's options are the ones read).
                Retry = RetryOptions.Default() with { Timeout = PerAttemptTimeout },
                Logging = new LoggingOptions
                {
                    // Explicit factory disarms UPVEST_INVESTMENT_API_LOG=trace (which would force
                    // request-body logging). Flow 1 carries heavy PII, so bodies are never logged.
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false
                }
            };
            options.Server.Default.Production.BaseUrl = settings.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddScoped<IInvestorPortfolioService, UpvestPortfolioService>();

        return services;
    }
}
