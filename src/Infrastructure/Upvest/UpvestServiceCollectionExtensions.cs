using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    /// <summary>
    /// Registers the whole "invest your change" capability: the Upvest client and its single
    /// authenticating handler, the domain services, and the background reconciler and webhook
    /// registrar.
    /// </summary>
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        // Authentication building blocks shared by every Upvest call.
        services.AddSingleton<UpvestRequestSigner>();
        services.AddSingleton<IUpvestTokenProvider, UpvestTokenProvider>();
        services.AddTransient<UpvestAuthenticationHandler>();

        var baseUrl = configuration[$"{UpvestSettings.SectionName}:BaseUrl"];
        services.AddHttpClient(UpvestHttpClient.Name, client =>
        {
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            }
        }).AddHttpMessageHandler<UpvestAuthenticationHandler>();

        // Upvest integration + domain orchestration. The gateway only depends on the HttpClient
        // factory and options, so it is a singleton; the mutation gate serialises investor writes.
        services.AddSingleton<IUpvestGateway, UpvestGateway>();
        services.AddSingleton<IInvestorMutationGate, InvestorMutationGate>();
        services.AddScoped<IInvestingService, InvestingService>();
        services.AddScoped<IOrderPlacementService, OrderPlacementService>();

        // Inbound webhooks (complementary to the authoritative reconciler).
        services.AddScoped<IUpvestWebhookProcessor, UpvestWebhookProcessor>();
        services.AddSingleton<IUpvestWebhookVerifier, UpvestWebhookVerifier>();

        services.AddHostedService<InvestmentReconciliationService>();
        services.AddHostedService<UpvestWebhookRegistrar>();

        return services;
    }
}
