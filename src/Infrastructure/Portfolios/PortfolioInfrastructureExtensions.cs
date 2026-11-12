using System;
using Microsoft.eShopWeb.ApplicationCore.Portfolios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Portfolios;

/// <summary>
/// Wires up the managed-portfolio integration: Upvest settings, the signed HTTP client and token
/// service, the Upvest client, the in-memory stores, and the orchestration service.
/// </summary>
public static class PortfolioInfrastructureExtensions
{
    public static IServiceCollection AddManagedPortfolios(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        services.AddSingleton<UpvestRequestSigner>();

        services.AddHttpClient<UpvestHttpClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
            }
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddSingleton<UpvestTokenService>();
        services.AddSingleton<IUpvestInvestmentClient, UpvestInvestmentClient>();

        services.AddSingleton<IInvestorStore, InMemoryInvestorStore>();
        services.AddSingleton<IModelPortfolioStore, InMemoryModelPortfolioStore>();
        services.AddSingleton<IPaymentStore, InMemoryPaymentStore>();
        services.AddSingleton<IRealignmentStore, InMemoryRealignmentStore>();

        services.AddScoped<IPortfolioService, PortfolioService>();

        return services;
    }
}
