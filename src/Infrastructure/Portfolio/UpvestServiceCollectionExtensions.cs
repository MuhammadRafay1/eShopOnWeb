using System;
using System.IO;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.eShopWeb.Infrastructure.Portfolio.Services;
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
    private const string HttpClientName = "Upvest";

    /// <summary>
    /// Registers the managed-portfolio capability: the app's own store (<see cref="PortfolioContext"/>),
    /// a long-lived signed Upvest SDK client bound from the <c>Upvest:</c> section, and the flow services.
    /// Fails startup fast if a required credential is missing/blank or the signing key file is absent.
    /// </summary>
    public static IServiceCollection AddUpvestPortfolioIntegration(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Bind + fail-fast validate the Upvest credentials at host start (not on first request).
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .Validate(ValidateSettings, FormatValidationFailure())
            .ValidateOnStart();

        // The app's own store. In-memory for this environment (no LocalDB); SQL Server otherwise.
        bool useOnlyInMemoryDatabase = false;
        if (configuration["UseOnlyInMemoryDatabase"] != null)
        {
            useOnlyInMemoryDatabase = bool.Parse(configuration["UseOnlyInMemoryDatabase"]!);
        }

        if (useOnlyInMemoryDatabase)
        {
            services.AddDbContext<PortfolioContext>(c => c.UseInMemoryDatabase("Portfolio"));
        }
        else
        {
            services.AddDbContext<PortfolioContext>(c =>
                c.UseSqlServer(configuration.GetConnectionString("CatalogConnection")));
        }

        // A dedicated, named HttpClient so this SDK's timeout/handler are not shared with other
        // unnamed consumers; PooledConnectionLifetime keeps DNS fresh behind the long-lived client.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        // One long-lived, signed SDK client. Options are built once (a rotated secret/key needs a restart).
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var options = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId!,
                    ClientSecret = settings.ClientSecret!
                },
                // Upvest refuses any unsigned request (the token request included); the client always signs.
                Hooks = new SdkHook[]
                {
                    new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(
                        keyId: settings.SigningKeyId!,
                        privateKeyPath: settings.SigningKeyPath!,
                        upvestClientId: settings.ClientId!,
                        privateKeyPassphrase: string.IsNullOrEmpty(settings.SigningKeyPassphrase)
                            ? null
                            : settings.SigningKeyPassphrase))
                },
                // Personal data flows through onboarding: never log request bodies/headers.
                Logging = new LoggingOptions
                {
                    LoggerFactory = loggerFactory,
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false
                },
                // Interactive path: cap one attempt well under the 100s default.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) }
            };

            // Upvest:BaseUrl is the base address for every call — used verbatim.
            options.Server.Default.Production.BaseUrl = settings.BaseUrl!;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddScoped<IInvestorService, InvestorService>();
        services.AddScoped<IModelPortfolioService, ModelPortfolioService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IRealignmentService, RealignmentService>();
        services.AddScoped<IPortfolioService, PortfolioQueryService>();

        return services;
    }

    private static bool ValidateSettings(UpvestSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.ClientId)) return false;
        if (string.IsNullOrWhiteSpace(s.ClientSecret)) return false;
        if (string.IsNullOrWhiteSpace(s.SigningKeyId)) return false;
        if (string.IsNullOrWhiteSpace(s.SigningKeyPath)) return false;
        if (string.IsNullOrWhiteSpace(s.BaseUrl)) return false;
        // Passphrase may legitimately be empty (unencrypted key); do not require it.
        if (!File.Exists(s.SigningKeyPath)) return false;
        return true;
    }

    // Names the missing keys, never their values.
    private static string FormatValidationFailure() =>
        "Upvest configuration is incomplete. Set non-blank values for Upvest:ClientId, Upvest:ClientSecret, " +
        "Upvest:SigningKeyId, Upvest:SigningKeyPath (pointing at an existing PEM) and Upvest:BaseUrl " +
        "(via user-secrets or environment). The signing key passphrase is optional.";
}
