using System;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;
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

/// <summary>
/// Registers the managed-portfolio capability: the Upvest SDK client (OAuth + required HTTP message
/// signing, base URL applied verbatim, credential fail-fast), the application's own EF store, the
/// gateway, and the application services.
/// </summary>
public static class PortfolioServiceCollectionExtensions
{
    private const string HttpClientName = "UpvestInvestmentApi";

    public static IServiceCollection AddManagedPortfolio(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind credentials and fail host start-up (not the first request) when one is missing/blank.
        services.AddSingleton<IValidateOptions<UpvestSettings>, UpvestSettingsValidator>();
        // The custom UpvestSettingsValidator enforces the required credentials and the key file's
        // existence; ValidateOnStart makes those checks run at host start-up, not on first request.
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .ValidateOnStart();

        // The application's own store for the capability (isolated from catalog/identity).
        var useOnlyInMemory = configuration["UseOnlyInMemoryDatabase"] is { } flag && bool.TryParse(flag, out var b) && b;
        if (useOnlyInMemory)
        {
            services.AddDbContext<PortfolioContext>(o => o.UseInMemoryDatabase("Portfolio"));
        }
        else
        {
            services.AddDbContext<PortfolioContext>(o => o.UseSqlServer(configuration.GetConnectionString("CatalogConnection")));
        }

        services.AddScoped(typeof(IPortfolioRepository<>), typeof(PortfolioRepository<>));

        // One long-lived SDK client over a named HttpClient: bounded per-attempt timeout, recycled
        // connections (fresh DNS), OAuth client credentials, mandatory HTTP message signing, and the
        // configured base URL used verbatim. The options object is built once at registration.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var passphrase = string.IsNullOrEmpty(settings.SigningKeyPassphrase) ? null : settings.SigningKeyPassphrase;

            var options = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret
                },
                Hooks = new SdkHook[]
                {
                    new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(
                        keyId: settings.SigningKeyId,
                        privateKeyPath: settings.SigningKeyPath,
                        upvestClientId: settings.ClientId,
                        privateKeyPassphrase: passphrase))
                },
                // Bound a single attempt; the whole-call budget comes from the ASP.NET request token.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(20) },
                // Never log request bodies (PII); set the logger explicitly so the env-var trace mode
                // cannot force body logging on.
                Logging = new LoggingOptions
                {
                    LoggerFactory = loggerFactory,
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false
                }
            };
            options.Server.Default.Production.BaseUrl = settings.BaseUrl;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddScoped<IUpvestInvestmentGateway, UpvestInvestmentGateway>();

        services.AddScoped<IInvestorService, InvestorService>();
        services.AddScoped<IModelPortfolioService, ModelPortfolioService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IRealignmentService, RealignmentService>();
        services.AddScoped<IPortfolioQueryService, PortfolioQueryService>();

        return services;
    }
}
