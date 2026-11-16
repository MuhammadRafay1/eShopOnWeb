using System;
using System.IO;
using System.Net.Http;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Core.Hooks;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.PublicApi.Portfolio;

public static class UpvestServiceCollectionExtensions
{
    private const string HttpClientName = "Upvest";

    /// <summary>
    /// Binds the <c>Upvest:</c> settings (failing startup if a required value is missing or the
    /// signing key file is absent), registers a singleton Upvest SDK client configured with OAuth
    /// client-credentials, HTTP message signing, the configured base URL, and body logging disabled
    /// so no personal data reaches the logs; then registers the gateway and the portfolio services.
    /// </summary>
    public static IServiceCollection AddUpvestInvestment(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(s => File.Exists(s.SigningKeyPath),
                "Upvest:SigningKeyPath does not point to an existing signing key file.")
            .ValidateOnStart();

        // A named HttpClient keeps this SDK off the app's shared default client, bounds one attempt,
        // and recycles pooled connections so DNS stays fresh behind the long-lived client.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            var diagnosticsLogger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                .CreateLogger("Upvest.Provider");

            var signingCredentials = UpvestHttpSignatureCredentials.FromFile(
                keyId: settings.SigningKeyId,
                privateKeyPath: settings.SigningKeyPath,
                upvestClientId: settings.ClientId,
                privateKeyPassphrase: settings.SigningKeyPassphrase);

            var options = new UpvestInvestmentApiClientOptions
            {
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret,
                },
                // Request signing is mandatory on every call, including the token request. The
                // response hook records the provider's error detail and correlation id on failures
                // (the provider's own error body, never our request body) for observability.
                Hooks = new SdkHook[]
                {
                    new UpvestSigningHook(signingCredentials),
                    SdkHook.OnResponse(async (response, context, ct) =>
                    {
                        if (response.IsSuccessStatusCode) return;
                        string body;
                        try { body = await response.Content.ReadAsStringAsync(); }
                        catch { body = "<unreadable>"; }
                        if (body.Length > 2000) body = body.Substring(0, 2000);
                        diagnosticsLogger.LogWarning(
                            "Upvest {Method} {Path} -> {Status}: {Body}",
                            context.Method, context.Uri.GetLeftPart(System.UriPartial.Path), (int)response.StatusCode, body);
                    }),
                },
                // Bound one attempt; a per-request CancellationToken bounds the whole call in services.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(20) },
                // Explicit factory disarms the UPVEST_INVESTMENT_API_LOG env var and keeps bodies
                // (which carry PII) out of the logs.
                Logging = new LoggingOptions
                {
                    LoggerFactory = NullLoggerFactory.Instance,
                    LogRequestBody = false,
                },
            };

            // Use the configured base URL verbatim instead of any default. The SDK's Production
            // environment is the sandbox; each deployment points Upvest:BaseUrl at its own host.
            options.Environment = UpvestInvestmentApi.Servers.ServerEnvironment.Production;
            options.Server.Default.Production.BaseUrl = settings.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddScoped<IUpvestInvestmentGateway, UpvestInvestmentGateway>();
        services.AddScoped<IInvestorService, InvestorService>();
        services.AddScoped<IModelPortfolioService, ModelPortfolioService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IRealignmentService, RealignmentService>();
        services.AddScoped<IPortfolioService, PortfolioService>();

        return services;
    }
}
