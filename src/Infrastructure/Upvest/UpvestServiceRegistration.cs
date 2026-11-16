using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Servers;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Registers the managed-portfolio capability: the Upvest SDK client (OAuth + request signing, bound from
/// the <c>Upvest:</c> configuration section), the gateway, and the application services. Fails fast at
/// startup when a required credential is missing, rather than on the first call in production.
/// </summary>
public static class UpvestServiceRegistration
{
    public static IServiceCollection AddManagedPortfolio(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new UpvestPortfolioSettings();
        configuration.GetSection(UpvestPortfolioSettings.SectionName).Bind(settings);

        // Fail-fast: refuse to start when a required credential is missing or blank. The passphrase may be
        // legitimately empty (an unencrypted key), so it is not required. Messages name the config key, never
        // the value.
        RequireConfigured(settings.ClientId, "Upvest:ClientId");
        RequireConfigured(settings.ClientSecret, "Upvest:ClientSecret");
        RequireConfigured(settings.SigningKeyId, "Upvest:SigningKeyId");
        RequireConfigured(settings.SigningKeyPath, "Upvest:SigningKeyPath");
        RequireConfigured(settings.BaseUrl, "Upvest:BaseUrl");

        services.AddUpvestInvestmentApiClient(options =>
        {
            options.Environment = ServerEnvironment.Production; // sandbox/live chosen by Upvest:BaseUrl
            options.Server.Default.Production.BaseUrl = settings.BaseUrl!; // use the configured base URL verbatim

            options.OauthClientCredentials = new OAuth2ClientCredentials
            {
                ClientId = settings.ClientId!,
                ClientSecret = settings.ClientSecret!
            };

            // Request signing is required on every Upvest call (including the token request).
            options.Hooks = new[]
            {
                new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile(
                    keyId: settings.SigningKeyId!,
                    privateKeyPath: settings.SigningKeyPath!,
                    upvestClientId: settings.ClientId!,
                    privateKeyPassphrase: string.IsNullOrEmpty(settings.SigningKeyPassphrase) ? null : settings.SigningKeyPassphrase))
            };

            // Bound per-attempt timeout; the gateway additionally bounds the whole call.
            // Request bodies carry personal data, so body logging stays off (the default).
            options.Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) };
        });

        services.AddScoped<IUpvestInvestmentGateway, UpvestInvestmentGateway>();
        services.AddScoped<IPortfolioInvestorService, PortfolioInvestorService>();
        services.AddScoped<IModelPortfolioService, ModelPortfolioService>();
        services.AddScoped<IPortfolioPaymentService, PortfolioPaymentService>();
        services.AddScoped<IPortfolioRealignmentService, PortfolioRealignmentService>();
        services.AddScoped<IPortfolioQueryService, PortfolioQueryService>();

        return services;
    }

    private static void RequireConfigured(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} is not configured. Set it via environment variable, user-secrets, or your secret store before starting the app.");
        }
    }
}
