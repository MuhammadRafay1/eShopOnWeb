using System;
using System.IO;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Servers;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Wires the Upvest managed-portfolio integration: the SDK client (OAuth + required request signing), the
/// gateway, the orchestration service, and a startup credential check that refuses to boot on a missing secret.
/// </summary>
public static class UpvestServiceCollectionExtensions
{
    private const string HttpClientName = "UpvestInvestmentApi";

    public static IServiceCollection AddUpvestPortfolioIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind the Upvest section and refuse to start if a required credential is missing or blank — so a
        // configuration fault surfaces at startup, not as an auth failure on the first call in production.
        services.AddOptions<UpvestOptions>()
            .Bind(configuration.GetSection(UpvestOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "Upvest:ClientId is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), "Upvest:ClientSecret is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKeyId), "Upvest:SigningKeyId is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKeyPath), "Upvest:SigningKeyPath is not configured.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Upvest:BaseUrl is not configured.")
            .Validate(o => File.Exists(o.SigningKeyPath),
                "Upvest:SigningKeyPath does not point to an existing signing-key file.")
            .ValidateOnStart();

        // A named HttpClient keeps the SDK's pipeline off the shared default client. Timeout bounds one attempt;
        // PooledConnectionLifetime keeps DNS fresh behind the long-lived singleton client below.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<UpvestOptions>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);

            var signingCredentials = UpvestHttpSignatureCredentials.FromFile(
                keyId: opts.SigningKeyId,
                privateKeyPath: opts.SigningKeyPath,
                upvestClientId: opts.ClientId,
                privateKeyPassphrase: string.IsNullOrEmpty(opts.SigningKeyPassphrase) ? null : opts.SigningKeyPassphrase);

            var clientOptions = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = opts.ClientId,
                    ClientSecret = opts.ClientSecret
                },
                // Upvest rejects any unsigned request (including the token request): sign every call.
                Hooks = [new UpvestSigningHook(signingCredentials)],
                // Bound one attempt; the whole-call budget is the caller's CancellationToken (RequestAborted).
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(30) }
            };
            // Use the configured base URL verbatim rather than relying on the environment default.
            clientOptions.Server.Default.Production.BaseUrl = opts.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, clientOptions);
        });

        services.AddScoped<IUpvestPortfolioGateway, UpvestPortfolioGateway>();
        services.AddScoped<IPortfolioService, PortfolioService>();

        return services;
    }
}
