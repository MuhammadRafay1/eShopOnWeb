using System;
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

public static class UpvestServiceCollectionExtensions
{
    private const string HttpClientName = "Upvest";

    /// <summary>
    /// Registers the Upvest Investment API client (one long-lived singleton, over a dedicated named
    /// <see cref="HttpClient"/>) and the application's <see cref="IUpvestInvestmentService"/> gateway.
    /// Binds the <c>Upvest</c> configuration section and refuses to start if a credential is missing.
    /// </summary>
    public static IServiceCollection AddUpvestInvestment(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<UpvestSettings>, UpvestSettingsValidator>();

        // A dedicated, unshared HttpClient pipeline. Timeout bounds one attempt; PooledConnectionLifetime
        // keeps DNS fresh behind the long-lived singleton client.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value; // validated at startup
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);

            var options = new UpvestInvestmentApiClientOptions
            {
                // Pin the Production node and override its base URL with the configured value, verbatim.
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId!,
                    ClientSecret = settings.ClientSecret!,
                    // When configured, request every token with this single scope (see UpvestSettings.Scope).
                    Scope = string.IsNullOrWhiteSpace(settings.Scope) ? null : settings.Scope
                },
                // Upvest signs every request (incl. the token call) with an HTTP message signature.
                Hooks =
                [
                    new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(
                        keyId: settings.SigningKeyId!,
                        privateKeyPath: settings.SigningKeyPath!,
                        upvestClientId: settings.ClientId!,
                        privateKeyPassphrase: string.IsNullOrEmpty(settings.SigningKeyPassphrase)
                            ? null
                            : settings.SigningKeyPassphrase))
                ],
                // Bound each attempt; the gateway also links the request's CancellationToken for the total.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(20) },
                // Explicit logger factory; request/response bodies are never logged (they carry personal data).
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false
                }
            };
            options.Server.Default.Production.BaseUrl = settings.BaseUrl!;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddScoped<IUpvestInvestmentService, UpvestInvestmentService>();

        return services;
    }
}
