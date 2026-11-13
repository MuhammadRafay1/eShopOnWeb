using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Core.Hooks;
using UpvestInvestmentApi.Servers;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.Infrastructure.ManagedPortfolio;

public static class UpvestServiceCollectionExtensions
{
    private const string HttpClientName = "Upvest";

    /// <summary>
    /// Registers the Upvest Investment API SDK client and the managed-portfolio service.
    ///
    /// <para>All six <c>Upvest:*</c> settings are bound from configuration and validated at startup
    /// (<see cref="OptionsBuilderDataAnnotationsExtensions.ValidateDataAnnotations{T}"/> +
    /// <c>ValidateOnStart</c>): the host refuses to boot if any is missing or blank, rather than discovering
    /// it on the first call. The SDK client is a singleton built once at registration — a rotated secret
    /// takes effect only on restart.</para>
    ///
    /// <para>Credentials map onto the SDK as: <c>Upvest:ClientId</c> → OAuth2 client id <em>and</em> the
    /// signing hook's Upvest client id; <c>Upvest:ClientSecret</c> → OAuth2 client secret;
    /// <c>Upvest:SigningKeyId/Path/Passphrase</c> → the HTTP-message-signing key; <c>Upvest:BaseUrl</c> →
    /// the base URL for every call, used verbatim.</para>
    /// </summary>
    public static IServiceCollection AddUpvestManagedPortfolio(this IServiceCollection services, IConfiguration configuration)
    {
        // Fail fast: every one of the six settings must be present and non-blank, each checked individually
        // (a blank part is not a missing one). ValidateOnStart runs these at boot, before the app serves,
        // so a missing secret is a startup failure, not an AuthSchemeException on the first call. No value is
        // ever echoed — only the key name.
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.ClientId), "Upvest:ClientId is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.ClientSecret), "Upvest:ClientSecret is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SigningKeyId), "Upvest:SigningKeyId is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SigningKeyPath), "Upvest:SigningKeyPath is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SigningKeyPassphrase), "Upvest:SigningKeyPassphrase is not configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.BaseUrl), "Upvest:BaseUrl is not configured.")
            .ValidateOnStart();

        // A named HttpClient keeps this SDK's pipeline off the shared default client. PooledConnectionLifetime
        // keeps DNS fresh behind the long-lived singleton; Timeout bounds one attempt.
        services.AddHttpClient(HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            var httpClient = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>().CreateClient(HttpClientName);

            var options = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret
                },
                // Upvest signs EVERY call (including the OAuth token request) with HTTP message signatures.
                Hooks = new List<SdkHook>
                {
                    new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(
                        keyId: settings.SigningKeyId,
                        privateKeyPath: settings.SigningKeyPath,
                        upvestClientId: settings.ClientId,
                        privateKeyPassphrase: settings.SigningKeyPassphrase))
                },
                // Per-attempt bound; POST/PATCH/DELETE stay non-retried by default so writes are not resent.
                Retry = RetryOptions.Default() with
                {
                    Timeout = TimeSpan.FromSeconds(10),
                    MaxRetries = 2
                },
                // Sensitive data: never log bodies/headers, and set LoggerFactory explicitly so the
                // UPVEST_INVESTMENT_API_LOG env var can never force body logging on.
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false
                }
            };

            // Upvest:BaseUrl is the base address for every call, used verbatim.
            options.Server.Default.Production.BaseUrl = settings.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddScoped<IManagedPortfolioService, ManagedPortfolioService>();

        return services;
    }

    /// <summary>
    /// Forces construction of the SDK client so a bad signing-key path/passphrase surfaces at startup rather
    /// than on the first request. Call after the host is built.
    /// </summary>
    public static void ValidateUpvestClient(this IServiceProvider services)
    {
        _ = services.GetRequiredService<UpvestInvestmentApiClient>();
    }
}
