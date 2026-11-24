using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Upvest.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Registers the Upvest integration: settings binding, the request signer and signing handler,
/// the token provider, the API client and the managed-portfolio application service.
/// </summary>
public static class UpvestServiceCollectionExtensions
{
    public static IServiceCollection AddUpvestIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        services.AddSingleton<UpvestMessageSigner>();
        services.AddTransient<UpvestSigningHandler>();
        services.AddSingleton<UpvestTokenProvider>();
        services.AddScoped<IUpvestInvestmentService, UpvestInvestmentService>();
        services.AddScoped<IManagedPortfolioService, ManagedPortfolioService>();

        services.AddHttpClient(UpvestHttp.ClientName, (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                throw new InvalidOperationException("Upvest:BaseUrl is not configured.");
            }
            var baseUrl = settings.BaseUrl.EndsWith('/') ? settings.BaseUrl : settings.BaseUrl + "/";
            client.BaseAddress = new Uri(baseUrl);
        })
        .AddHttpMessageHandler<UpvestSigningHandler>();

        return services;
    }
}
