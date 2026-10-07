using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

public static class InvestingServiceExtensions
{
    /// <summary>
    /// Registers the "invest your change" capability: the Upvest settings binding, the single
    /// reusable Upvest client, the provider adapter, the orchestration service, and the
    /// best-effort webhook registrar.
    /// </summary>
    public static IServiceCollection AddInvestingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(settings =>
            configuration.GetSection(UpvestSettings.SectionName).Bind(settings));

        services.AddSingleton<UpvestClient>();
        services.AddScoped<IInvestmentProvider, UpvestInvestmentProvider>();
        services.AddScoped<IInvestingService, InvestingService>();
        services.AddScoped<IOrderPlacementService, OrderPlacementService>();

        return services;
    }
}
