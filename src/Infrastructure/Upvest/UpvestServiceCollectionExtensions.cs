using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Registers the Upvest integration: options (bound from the <c>Upvest:</c> section), the signed
/// HTTP client and token provider, the typed <see cref="IUpvestClient"/>, and the portfolio service.
/// </summary>
public static class UpvestServiceCollectionExtensions
{
    public static IServiceCollection AddUpvestPortfolioIntegration(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestOptions>(configuration.GetSection(UpvestOptions.SectionName));

        services.AddHttpClient(UpvestHttp.ClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpvestOptions>>().Value;
            if (!string.IsNullOrEmpty(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            }
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Key loading and token caching are process-wide concerns.
        services.AddSingleton<UpvestMessageSigner>();
        services.AddSingleton<UpvestTokenProvider>();

        services.AddScoped<IUpvestClient, UpvestClient>();
        services.AddScoped<IPortfolioService, PortfolioService>();

        return services;
    }
}
