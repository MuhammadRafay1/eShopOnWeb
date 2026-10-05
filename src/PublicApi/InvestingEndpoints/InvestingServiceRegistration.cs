using System;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public static class InvestingServiceRegistration
{
    /// <summary>
    /// Wires the "invest your change" feature: Upvest settings (bound from the <c>Upvest:</c>
    /// section), the single authenticating <see cref="UpvestAuthenticationHandler"/>, the typed
    /// Upvest clients, the investing service and its background worker.
    /// </summary>
    public static IServiceCollection AddInvesting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new UpvestSettings();
        configuration.GetSection(UpvestSettings.SectionName).Bind(settings);
        services.AddSingleton(settings);

        services.AddSingleton<UpvestMessageSigner>();
        services.AddSingleton<UpvestTokenStore>();
        services.AddTransient<UpvestAuthenticationHandler>();

        var baseUri = Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var parsed)
            ? parsed
            : new Uri("http://localhost:9");

        services.AddHttpClient<IUpvestClient, UpvestClient>(c => c.BaseAddress = baseUri)
            .AddHttpMessageHandler<UpvestAuthenticationHandler>();

        services.AddHttpClient<UpvestWebhookVerifier>(c => c.BaseAddress = baseUri)
            .AddHttpMessageHandler<UpvestAuthenticationHandler>();

        services.AddScoped<IInvestingService, InvestingService>();
        services.AddHostedService<InvestingBackgroundService>();

        return services;
    }
}
