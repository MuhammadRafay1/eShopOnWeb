using System;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.PublicApi.Investing.Upvest;
using Microsoft.eShopWeb.PublicApi.OrderEndpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Investing;

public static class InvestingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the "invest your change" capability: the Upvest client (behind the single
    /// authenticating handler), the investing application services and the reconciliation worker.
    /// </summary>
    public static IServiceCollection AddInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.Configure<UpvestOptions>(configuration.GetSection(UpvestOptions.ConfigSection));

        services.AddSingleton<UpvestMessageSigner>();
        services.AddSingleton<UpvestTokenStore>();
        services.AddTransient<UpvestAuthenticationHandler>();

        // Every call to Upvest flows through the typed client, whose pipeline contains the single
        // authenticating DelegatingHandler. No call site attaches credentials itself.
        services.AddHttpClient<IUpvestApiClient, UpvestApiClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<UpvestOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                {
                    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
                }
            })
            .AddHttpMessageHandler<UpvestAuthenticationHandler>();

        services.AddScoped<IInvestingService, InvestingService>();
        services.AddScoped<IOrderPlacementService, OrderPlacementService>();
        services.AddScoped<UpvestWebhookProcessor>();

        services.AddHostedService<InvestingReconciliationService>();

        return services;
    }
}
