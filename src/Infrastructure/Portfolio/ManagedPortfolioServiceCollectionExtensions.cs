using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Core.Hooks;
using UpvestInvestmentApi.Servers;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

public static class ManagedPortfolioServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Upvest-backed managed-portfolio capability: binds and validates the
    /// <c>Upvest:</c> configuration (failing startup fast when a credential is missing), builds the
    /// SDK client (OAuth2 client credentials + HTTP message signing + the configured base URL), and
    /// registers <see cref="IManagedPortfolioService"/>.
    /// </summary>
    public static IServiceCollection AddManagedPortfolio(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new UpvestSettings();
        configuration.GetSection(UpvestSettings.SectionName).Bind(settings);
        ValidateFailFast(settings);

        services.AddSingleton(settings);

        // Build the signing credentials once, at registration, and capture them in the singleton
        // client (a rotated secret or passphrase takes effect only on restart — documented behaviour).
        services.AddUpvestInvestmentApiClient(options =>
        {
            options.Environment = ServerEnvironment.Production; // sandbox host; the live host is Environment2
            options.Server.Default.Production.BaseUrl = settings.BaseUrl; // used verbatim for every call

            options.OauthClientCredentials = new OAuth2ClientCredentials
            {
                ClientId = settings.ClientId,
                ClientSecret = settings.ClientSecret
            };

            var signingCredentials = UpvestHttpSignatureCredentials.FromFile(
                keyId: settings.SigningKeyId,
                privateKeyPath: settings.SigningKeyPath,
                upvestClientId: settings.ClientId,
                privateKeyPassphrase: string.IsNullOrEmpty(settings.SigningKeyPassphrase) ? null : settings.SigningKeyPassphrase);

            options.Hooks = new List<SdkHook> { new UpvestSigningHook(signingCredentials) };

            // Per-attempt timeout (the default 100s is far too long for a request path). POST/PATCH/DELETE
            // are not auto-retried by default, which keeps non-idempotent writes from executing twice.
            options.Retry = RetryOptions.Default() with
            {
                Timeout = TimeSpan.FromSeconds(settings.AttemptTimeoutSeconds)
            };

            // Personal data and money flow through these calls: never log request bodies. LoggerFactory is
            // filled from the host's container by the DI extension, which also disarms the env-var trace path.
            options.Logging = options.Logging with { LogRequestBody = false };
        });

        services.AddScoped<IManagedPortfolioService, UpvestManagedPortfolioService>();

        return services;
    }

    private static void ValidateFailFast(UpvestSettings s)
    {
        RequireValue(s.ClientId, "Upvest:ClientId");
        RequireValue(s.ClientSecret, "Upvest:ClientSecret");
        RequireValue(s.SigningKeyId, "Upvest:SigningKeyId");
        RequireValue(s.SigningKeyPath, "Upvest:SigningKeyPath");
        RequireValue(s.BaseUrl, "Upvest:BaseUrl");

        if (!s.SigningKeyIsUnencrypted)
        {
            RequireValue(s.SigningKeyPassphrase, "Upvest:SigningKeyPassphrase");
        }

        if (!Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Upvest:BaseUrl is not a valid absolute URL. Set it before starting the app.");
        }
    }

    private static void RequireValue(string value, string key)
    {
        // Names the missing key; never echoes the value (present or absent).
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} is not configured. Set it via environment variable, user-secrets, or your secret store before starting the app.");
        }
    }
}
