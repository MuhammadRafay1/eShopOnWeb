using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Investing.Http;
using Microsoft.eShopWeb.Infrastructure.Investing.Webhooks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Registers everything for the "invest your change" capability: the single authenticating
/// handler and the Upvest HTTP client, the domain services, and the background workers.
/// </summary>
public static class InvestingServiceCollectionExtensions
{
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));
        var baseUrl = configuration[$"{UpvestSettings.SectionName}:BaseUrl"];

        // Authentication building blocks.
        services.AddSingleton<IUpvestMessageSigner, UpvestMessageSigner>();
        services.AddSingleton<IUpvestTokenProvider, UpvestTokenProvider>();
        services.AddTransient<UpvestAuthenticationHandler>();

        // The single HttpClient every Upvest call flows through; its only message handler is the
        // one that authenticates the call (OAuth bearer + HTTP message signature).
        services.AddHttpClient(UpvestConstants.HttpClientName, client =>
        {
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }
        })
        .AddHttpMessageHandler<UpvestAuthenticationHandler>();

        services.AddSingleton<IUpvestInvestmentClient, UpvestInvestmentClient>();
        services.AddSingleton<InvestingConcurrencyGuard>();

        services.AddScoped<IInvestorOnboardingService, InvestorOnboardingService>();
        services.AddScoped<IInvestingProcessor, InvestingProcessor>();
        services.AddScoped<IUpvestWebhookService, UpvestWebhookService>();

        services.AddHostedService<InvestingBackgroundService>();
        services.AddHostedService<UpvestWebhookRegistrar>();

        return services;
    }
}
