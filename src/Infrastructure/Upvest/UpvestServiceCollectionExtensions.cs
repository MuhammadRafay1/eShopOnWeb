using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    /// <summary>
    /// Registers the "invest your change" integration: Upvest settings (bound from the
    /// <c>Upvest:</c> section), the signing/authentication pipeline, the typed Upvest client, and
    /// the investing service. Every Upvest call flows through the one authenticating handler.
    /// </summary>
    public static IServiceCollection AddInvestingIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        var upvestSection = configuration.GetSection(UpvestSettings.SectionName);
        services.Configure<UpvestSettings>(upvestSection);

        var settings = upvestSection.Get<UpvestSettings>() ?? new UpvestSettings();

        // Product settings that the domain service needs (instrument to buy, currency, threshold).
        services.AddSingleton(new InvestingSettings { InstrumentId = settings.InstrumentId });

        services.AddSingleton<IUpvestRequestSigner, UpvestRequestSigner>();
        services.AddSingleton<IUpvestTokenProvider, UpvestTokenProvider>();
        services.AddTransient<UpvestAuthenticationHandler>();

        // Dedicated client for the token request. It runs through the SAME authenticating handler,
        // which signs the token request without a bearer (there is no token yet).
        services.AddHttpClient(UpvestTokenProvider.HttpClientName, client =>
        {
            if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                client.BaseAddress = new Uri(settings.BaseUrl);
            }
        })
        .AddHttpMessageHandler<UpvestAuthenticationHandler>();

        // Typed client for every other Upvest call — authenticated + signed by the single handler.
        services.AddHttpClient<IUpvestClient, UpvestClient>(client =>
        {
            if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                client.BaseAddress = new Uri(settings.BaseUrl);
            }
        })
        .AddHttpMessageHandler<UpvestAuthenticationHandler>();

        services.AddScoped<IInvestingService, InvestingService>();

        return services;
    }
}
