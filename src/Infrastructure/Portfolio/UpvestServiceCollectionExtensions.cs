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
using UpvestInvestmentApi.Core.Hooks;
using UpvestInvestmentApi.Servers;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

public static class UpvestServiceCollectionExtensions
{
    private const string HttpClientName = "UpvestInvestmentApi";

    /// <summary>
    /// Registers the Upvest client (OAuth2 + HTTP message signing), the in-memory portfolio store, and the
    /// portfolio gateway. Binds configuration from the <c>Upvest:</c> section and fails fast at startup when a
    /// required credential is missing or the signing key file is absent — never discovering it on first call.
    /// </summary>
    public static IServiceCollection AddUpvestPortfolio(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .ValidateOnStart();

        // Fail-fast: every required credential part present (data annotations) and the key file exists.
        services.AddSingleton<IValidateOptions<UpvestSettings>, UpvestSettingsValidator>();

        // A named HttpClient with a bounded per-attempt timeout and recycled connections (fresh DNS behind a
        // long-lived singleton client). Kept off the default client so no other consumer is affected.
        services.AddHttpClient(HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15); // per attempt; the gateway adds a whole-call budget
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);

            var passphrase = string.IsNullOrWhiteSpace(settings.SigningKeyPassphrase) ? null : settings.SigningKeyPassphrase;

            var options = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret,
                },
                Hooks = new SdkHook[]
                {
                    new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(
                        keyId: settings.SigningKeyId,
                        privateKeyPath: settings.SigningKeyPath,
                        upvestClientId: settings.ClientId,
                        privateKeyPassphrase: passphrase)),
                },
                // PII safety: body logging stays off and the logger factory is set explicitly so the
                // UPVEST_INVESTMENT_API_LOG environment variable cannot turn body logging on behind our backs.
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false,
                },
                // Per-attempt timeout; the gateway enforces the whole-call budget via a cancellation token.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) },
            };

            // Upvest:BaseUrl is the base address for every call (overrides the environment default).
            options.Server.Default.Production.BaseUrl = settings.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddSingleton<IPortfolioStore, InMemoryPortfolioStore>();
        services.AddSingleton<IUpvestPortfolioGateway, UpvestPortfolioGateway>();

        return services;
    }
}

/// <summary>Validates that every Upvest credential part is present and the signing key file exists.</summary>
internal sealed class UpvestSettingsValidator : IValidateOptions<UpvestSettings>
{
    public ValidateOptionsResult Validate(string? name, UpvestSettings options)
    {
        // Enforce the [Required]/[Range] data annotations (reports each missing credential key by name) ...
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
        var results = new System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, context, results, validateAllProperties: true))
        {
            var messages = new System.Collections.Generic.List<string>();
            foreach (var r in results)
            {
                if (r.ErrorMessage is not null)
                {
                    messages.Add(r.ErrorMessage);
                }
            }

            return ValidateOptionsResult.Fail(messages);
        }

        // ... then the file check (never echo any value).
        if (!File.Exists(options.SigningKeyPath))
        {
            return ValidateOptionsResult.Fail(
                "The Upvest signing key file at Upvest:SigningKeyPath does not exist.");
        }

        return ValidateOptionsResult.Success;
    }
}
