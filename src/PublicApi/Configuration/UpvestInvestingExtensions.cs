using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Investing;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.eShopWeb.PublicApi.InvestingEndpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Configuration;

public static class UpvestInvestingExtensions
{
    /// <summary>
    /// Wires up the "Invest your change" feature: the Upvest-authenticating
    /// HTTP client (single delegating handler), the investing services, and the
    /// background reconciliation worker. The worker only runs when an Upvest
    /// base URL is configured, so test hosts without credentials are unaffected.
    /// </summary>
    public static IServiceCollection AddUpvestInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(UpvestOptions.CONFIG_NAME);
        services.Configure<UpvestOptions>(section);
        var options = section.Get<UpvestOptions>() ?? new UpvestOptions();

        services.AddHttpContextAccessor();

        services.AddSingleton<UpvestRequestSigner>();
        services.AddSingleton<UpvestTokenStore>();
        services.AddTransient<UpvestAuthenticationHandler>();

        services.AddHttpClient<IUpvestClient, UpvestClient>((sp, client) =>
        {
            var upvest = sp.GetRequiredService<IOptions<UpvestOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(upvest.BaseUrl))
            {
                client.BaseAddress = new Uri(upvest.BaseUrl);
            }
        }).AddHttpMessageHandler<UpvestAuthenticationHandler>();

        services.AddScoped<IEnrolmentService, EnrolmentService>();
        services.AddScoped<IInvestingService, InvestingService>();
        services.AddScoped<IInvestmentReconciliationService, InvestmentReconciliationService>();

        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            services.AddHostedService<InvestingReconciliationBackgroundService>();
        }

        return services;
    }
}
