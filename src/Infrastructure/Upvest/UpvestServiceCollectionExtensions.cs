using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    /// <summary>
    /// Wire up the invest-your-change capability: the Upvest options, the single authenticating
    /// HttpClient, the typed Upvest gateway, the investing service and the background reconciler.
    /// </summary>
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestOptions>(configuration.GetSection(UpvestOptions.SectionName));

        // Business rule + the configured fund, taken from the Upvest options.
        services.AddSingleton(sp =>
        {
            var upvest = sp.GetRequiredService<IOptions<UpvestOptions>>().Value;
            return new InvestingOptions { InstrumentId = upvest.InstrumentId };
        });

        // Authentication building blocks.
        services.AddSingleton<UpvestMessageSigner>();
        services.AddSingleton<UpvestTokenProvider>();
        services.AddTransient<UpvestAuthenticationHandler>();

        // The one HttpClient every Upvest call flows through; the handler authenticates each call.
        services.AddHttpClient(UpvestTokenProvider.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<UpvestOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.BaseUrl))
                throw new InvalidOperationException("Upvest:BaseUrl is not configured.");
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        })
        .AddHttpMessageHandler<UpvestAuthenticationHandler>();

        services.AddScoped<IUpvestClient, UpvestClient>();
        services.AddScoped<IInvestingService, InvestingService>();

        services.AddHostedService<InvestmentReconciliationService>();

        return services;
    }
}
