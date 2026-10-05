using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Servers;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

public static class UpvestServiceCollectionExtensions
{
    /// <summary>
    /// Registers the "Invest your change" integration: the Upvest SDK client behind the one authenticating
    /// <see cref="UpvestAuthenticationHandler"/>, the gateway, the investing services and the background
    /// processor. Binds and validates the <c>Upvest:</c> settings, refusing to start if any are missing.
    /// </summary>
    public static IServiceCollection AddInvestingServices(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(UpvestOptions.SectionName).Get<UpvestOptions>() ?? new UpvestOptions();
        ValidateOrThrow(options);
        services.AddSingleton(options);

        services.AddSingleton<UpvestResponseCapture>();
        services.AddSingleton<UpvestRequestSigner>();
        services.AddSingleton<UpvestTokenProvider>();
        services.AddTransient<UpvestAuthenticationHandler>();

        services.AddHttpClient(UpvestAuthenticationHandler.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30); // per-attempt backstop; overall bound is the caller's token
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            .AddHttpMessageHandler<UpvestAuthenticationHandler>();

        services.AddSingleton(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(UpvestAuthenticationHandler.HttpClientName);
            var clientOptions = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = null, // auth is applied by UpvestAuthenticationHandler, not the SDK
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetService<ILoggerFactory>(),
                    LogRequestBody = false // request bodies carry personal data — never log them
                }
            };
            // Use the configured base URL verbatim (the local mock) instead of the SDK's sandbox default.
            clientOptions.Server.Default.Production.BaseUrl = options.BaseUrl;
            return new UpvestInvestmentApiClient(httpClient, clientOptions);
        });

        services.AddSingleton<IUpvestGateway, UpvestGateway>();
        services.AddSingleton<InvestmentQueue>();
        services.AddSingleton<IInvestmentQueue>(sp => sp.GetRequiredService<InvestmentQueue>());
        services.AddScoped<IEnrolmentReconciler, EnrolmentReconciler>();
        services.AddScoped<IInvestingService, InvestingService>();
        services.AddHostedService<InvestmentProcessor>();

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

        if (!Guid.TryParse(o.ClientId, out _))
        {
            throw new InvalidOperationException("Upvest:ClientId must be a valid client id (GUID).");
        }
        if (!File.Exists(o.SigningKeyPath))
        {
            throw new InvalidOperationException($"Upvest:SigningKeyPath points to a file that does not exist.");
        }
        try
        {
            using var key = ECDsa.Create();
            key.ImportFromEncryptedPem(File.ReadAllText(o.SigningKeyPath).AsSpan(), o.SigningKeyPassphrase.AsSpan());
        }
        catch (Exception ex)
        {
            // Never echo the passphrase or key material.
            throw new InvalidOperationException("Upvest signing key could not be loaded with the configured passphrase.", ex);
        }
    }

    private static void Require(string value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} is not configured. Set it via environment variable or user-secrets before starting the app.");
        }
    }
}
