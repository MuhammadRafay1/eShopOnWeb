using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Servers;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Upvest connection: the single authenticating DelegatingHandler, the token provider and
    /// signer, the SDK client (pointed at Upvest:BaseUrl), and the gateway. Validates configuration at
    /// startup and refuses to continue if a credential is missing or the signing key cannot be loaded.
    /// </summary>
    public static IServiceCollection AddUpvestIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(UpvestOptions.SectionName).Get<UpvestOptions>() ?? new UpvestOptions();
        ValidateOrThrow(options);

        services.AddSingleton(options);
        services.AddSingleton<UpvestResponseCapture>();
        services.AddSingleton<UpvestRequestSigner>();
        services.AddSingleton<UpvestTokenProvider>();
        services.AddTransient<UpvestAuthenticationHandler>();

        // Plain client used only to fetch the access token (must not recurse through the auth handler).
        services.AddHttpClient(UpvestTokenProvider.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15));

        // The SDK's HttpClient: our auth handler signs every call; PooledConnectionLifetime keeps a
        // long-lived singleton client's connections (and DNS) fresh.
        const string sdkClientName = "upvest-sdk";
        services.AddHttpClient(sdkClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .AddHttpMessageHandler<UpvestAuthenticationHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(sdkClientName);
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var clientOptions = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                // Logging on, body logging OFF (personal data), LoggerFactory set explicitly so the
                // UPVESTINVESTMENTAPICLIENT_LOG env var cannot switch body logging on from outside.
                Logging = new LoggingOptions
                {
                    LoggerFactory = loggerFactory,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false,
                    LogRequestBody = false,
                },
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) },
                // OauthClientCredentials deliberately unset: the auth handler owns token + signing.
            };
            clientOptions.Server.Default.Production.BaseUrl = options.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, clientOptions);
        });

        services.AddSingleton<IUpvestGateway, UpvestGateway>();
        services.AddSingleton<UpvestWebhookVerifier>();

        return services;
    }

    private static void ValidateOrThrow(UpvestOptions o)
    {
        Require(o.ClientId, "Upvest:ClientId");
        Require(o.ClientSecret, "Upvest:ClientSecret");
        Require(o.SigningKeyId, "Upvest:SigningKeyId");
        Require(o.SigningKeyPath, "Upvest:SigningKeyPath");
        Require(o.SigningKeyPassphrase, "Upvest:SigningKeyPassphrase");
        Require(o.BaseUrl, "Upvest:BaseUrl");
        Require(o.InstrumentId, "Upvest:InstrumentId");
        Require(o.CallbackBaseUrl, "Upvest:CallbackBaseUrl");

        if (!File.Exists(o.SigningKeyPath))
            throw new InvalidOperationException(
                $"Upvest:SigningKeyPath points to a file that does not exist. Configure it via user-secrets or environment before starting.");

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromEncryptedPem(File.ReadAllText(o.SigningKeyPath), o.SigningKeyPassphrase);
        }
        catch (Exception ex)
        {
            // Never echo the passphrase or key material.
            throw new InvalidOperationException(
                "Upvest signing key could not be loaded with the configured Upvest:SigningKeyPassphrase.", ex);
        }
    }

    private static void Require(string value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"{key} is not configured. Set it via user-secrets or environment variable before starting the app.");
    }
}
