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
    /// Registers the full "invest your change" integration: Upvest settings, the single signing
    /// DelegatingHandler, the typed Upvest client, the investing service, and the background
    /// reconciler.
    /// </summary>
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        services.AddSingleton<UpvestRequestSigner>();
        services.AddSingleton<IUpvestTokenProvider, UpvestTokenProvider>();
        services.AddTransient<UpvestSigningHandler>();

        services.AddHttpClient<IUpvestClient, UpvestClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<UpvestSettings>>().Value;
            if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
                client.BaseAddress = new Uri(settings.BaseUrl);
        })
        .AddHttpMessageHandler<UpvestSigningHandler>();

        services.AddScoped<IInvestingService, InvestingService>();
        services.AddHostedService<UpvestReconciliationService>();

        return services;
    }
}
