using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Upvest gateway, request signing, token service and the portfolio application
    /// service. Binds settings from the "Upvest" configuration section (values supplied via
    /// user-secrets / environment).
    /// </summary>
    public static IServiceCollection AddUpvestPortfolio(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        // The signing key is loaded (and decrypted) once and shared.
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            return UpvestSigningKey.Load(settings);
        });

        services.AddTransient<UpvestSigningHandler>();
        services.AddSingleton<UpvestTokenService>();

        services.AddHttpClient(UpvestTokenService.HttpClientName, (sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl);
            })
            .AddHttpMessageHandler<UpvestSigningHandler>();

        services.AddHttpClient<IUpvestGateway, UpvestGateway>((sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
                client.BaseAddress = new Uri(settings.BaseUrl);
            })
            .AddHttpMessageHandler<UpvestSigningHandler>();

        services.AddScoped<IPortfolioService, PortfolioService>();

        return services;
    }
}
