using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.eShopWeb.Infrastructure.Upvest.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Registers the Upvest integration: configuration binding, HTTP signing, token provider,
/// typed client, and the managed-portfolio orchestration service.
/// </summary>
public static class UpvestServiceCollectionExtensions
{
    public static IServiceCollection AddUpvestIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind the Upvest:* configuration section (values come from user-secrets / environment).
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        // The signing key is loaded once; a fresh ECDsa is created per signature (thread-safe).
        services.AddSingleton<IUpvestMessageSigner, UpvestMessageSigner>();

        services.AddHttpClient(UpvestRequestExecutor.HttpClientName);

        services.AddSingleton<UpvestRequestExecutor>();
        services.AddSingleton<UpvestTokenProvider>();
        services.AddSingleton<IUpvestClient, UpvestClient>();

        services.AddScoped<IManagedPortfolioService, ManagedPortfolioService>();

        return services;
    }
}
