using System;
using System.IO;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.PublicApi.Portfolio.Data;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;
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

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Configuration;

/// <summary>
/// Wires the managed-investment-portfolio capability into PublicApi: the Upvest SDK client (bound from the
/// <c>Upvest:</c> section, with OAuth2 + HTTP message signing), the PublicApi-owned <see cref="PortfolioContext"/>,
/// the gateway, and the per-flow application services. Credentials are validated at startup so a missing or blank
/// one refuses the boot rather than surfacing on the first call.
/// </summary>
public static class PortfolioServiceCollectionExtensions
{
    private const string HttpClientName = "UpvestInvestmentApi";

    public static IServiceCollection AddManagedPortfolios(this IServiceCollection services, IConfiguration configuration)
    {
        // Fail fast: bind the Upvest settings and validate at startup (not on first request). Every required part is
        // checked — a blank part is as fatal as a missing one — and the signing key file must exist.
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(s => File.Exists(s.SigningKeyPath),
                "Upvest:SigningKeyPath does not point to an existing file.")
            .ValidateOnStart();

        // Named HttpClient so the SDK gets its own pipeline: a per-attempt timeout backstop and a pooled-connection
        // lifetime that keeps DNS fresh behind the long-lived singleton client.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            });

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
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret,
                },
                // HTTP message signing is required on every call (incl. the token fetch).
                Hooks = new SdkHook[]
                {
                    new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(
                        keyId: settings.SigningKeyId,
                        privateKeyPath: settings.SigningKeyPath,
                        upvestClientId: settings.ClientId,
                        privateKeyPassphrase: string.IsNullOrEmpty(settings.SigningKeyPassphrase) ? null : settings.SigningKeyPassphrase)),
                },
                // Personal data travels on the user-create path: keep body logging OFF and set LoggerFactory
                // explicitly so the UPVEST_INVESTMENT_API_LOG env var cannot force unredacted bodies on.
                Logging = new LoggingOptions
                {
                    LoggerFactory = loggerFactory,
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false,
                },
                // Per-attempt timeout; the whole-call budget is enforced per request in the services.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) },
            };

            // Base URL is driven verbatim by configuration for the selected environment.
            options.Server.Default.Production.BaseUrl = settings.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        // PublicApi-owned, isolated store (in-memory per the deployment caveat; SQL otherwise).
        var useInMemory = configuration["UseOnlyInMemoryDatabase"] is { } v && bool.TryParse(v, out var b) && b;
        if (useInMemory)
        {
            services.AddDbContext<PortfolioContext>(o => o.UseInMemoryDatabase("PortfolioDb"));
        }
        else
        {
            services.AddDbContext<PortfolioContext>(o => o.UseSqlServer(
                configuration.GetConnectionString("PortfolioConnection")
                ?? configuration.GetConnectionString("CatalogConnection")));
        }

        services.AddScoped<IUpvestGateway, UpvestGateway>();
        services.AddScoped<InvestorService>();
        services.AddScoped<ModelPortfolioService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<RealignmentService>();
        services.AddScoped<PortfolioQueryService>();

        return services;
    }
}
