using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Investing.Upvest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Wires up the "invest your change" capability: Upvest options and the single reusable SDK client,
/// the gateway, the domain service, and the background reconciler. Call once from the host.
/// </summary>
public static class InvestingServiceExtensions
{
    public static IServiceCollection AddInvestingServices(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(UpvestOptions.SectionName);
        services.Configure<UpvestOptions>(options => section.Bind(options));

        // Captures raw Upvest responses the SDK's models cannot deserialize (provider/SDK model skew).
        services.AddSingleton<UpvestResponseCapture>();

        // One Upvest SDK client for the whole app (built once, reused). Authentication — the OAuth
        // grant and the request-signing handler — is configured entirely inside the factory.
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<UpvestOptions>>().Value;
            var capture = sp.GetRequiredService<UpvestResponseCapture>();
            return UpvestClientFactory.Create(options, capture);
        });

        services.AddScoped<IInvestingGateway, UpvestInvestingGateway>();
        services.AddScoped<IInvestingService, InvestingService>();

        services.AddHostedService<InvestmentReconciliationService>();

        return services;
    }
}
