using System;
using System.IO;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Servers;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// Registers the managed-portfolio capability: the Upvest SDK client (OAuth2 + required HTTP message
/// signing), the EF persistence context, the repository and the service. Credentials are validated at
/// registration time so the host refuses to start when one is missing — rather than failing the first call.
/// Call this only from the PublicApi host; the shared Infrastructure registration is left untouched so the
/// storefront and tests do not require Upvest configuration.
/// </summary>
public static class UpvestPortfolioServiceCollectionExtensions
{
    private const string HttpClientName = "UpvestInvestmentApi";

    public static IServiceCollection AddUpvestPortfolio(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = new UpvestSettings();
        configuration.GetSection(UpvestSettings.SectionName).Bind(settings);

        ValidateOrThrow(settings);

        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        // Persistence: use the same in-memory/SQL decision the rest of Infrastructure uses.
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

        services.AddScoped<IPortfolioRepository, EfPortfolioRepository>();

        // A named HttpClient keeps this SDK's pipeline off the shared default client, and
        // PooledConnectionLifetime keeps DNS fresh behind the long-lived singleton client.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        // The SDK client is a long-lived singleton (it caches the OAuth token and owns the retry pipeline).
        // Options are built once here, so a rotated secret takes effect only on process restart.
        services.AddSingleton(sp =>
        {
            var s = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var signingCredentials = UpvestHttpSignatureCredentials.FromFile(
                keyId: s.SigningKeyId!,
                privateKeyPath: s.SigningKeyPath!,
                upvestClientId: s.ClientId!,
                privateKeyPassphrase: string.IsNullOrEmpty(s.SigningKeyPassphrase) ? null : s.SigningKeyPassphrase);

            var options = new UpvestInvestmentApiClientOptions
            {
                Environment = ServerEnvironment.Production,
                OauthClientCredentials = new OAuth2ClientCredentials
                {
                    ClientId = s.ClientId!,
                    ClientSecret = s.ClientSecret!
                },
                Hooks = new[] { new UpvestSigningHook(signingCredentials) },
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(20) },
                // LoggerFactory is set explicitly so the UPVEST_INVESTMENT_API_LOG env var can never force
                // request-body logging on; LogRequestBody stays off because Flow 1 carries personal data.
                Logging = new LoggingOptions
                {
                    LoggerFactory = loggerFactory,
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false
                }
            };

            // Apply the configured base URL verbatim for the selected environment.
            options.Server.Default.Production.BaseUrl = s.BaseUrl!;

            return new UpvestInvestmentApiClient(httpClient, options);
        });

        services.AddScoped<IUpvestPortfolioService, UpvestPortfolioService>();

        return services;
    }

    /// <summary>
    /// Fail fast: every credential part must be present and non-blank (a blank part is not a missing one),
    /// and the signing key file must exist. Messages name the missing key and never echo a value.
    /// </summary>
    private static void ValidateOrThrow(UpvestSettings s)
    {
        RequireNonBlank(s.ClientId, "Upvest:ClientId");
        RequireNonBlank(s.ClientSecret, "Upvest:ClientSecret");
        RequireNonBlank(s.SigningKeyId, "Upvest:SigningKeyId");
        RequireNonBlank(s.SigningKeyPath, "Upvest:SigningKeyPath");
        RequireNonBlank(s.BaseUrl, "Upvest:BaseUrl");

        if (!File.Exists(s.SigningKeyPath))
        {
            throw new InvalidOperationException(
                $"The Upvest signing key file configured at Upvest:SigningKeyPath does not exist. " +
                $"Set Upvest:SigningKeyPath to a readable private-key file before starting the app.");
        }

        if (!Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                "Upvest:BaseUrl is not a valid absolute URL. Set it to the Upvest base address before starting the app.");
        }
    }

    private static void RequireNonBlank(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} is not configured. Set it via environment variable, user-secrets, or your secret store before starting the app.");
        }
    }
}
