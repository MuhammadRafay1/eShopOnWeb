using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Upvest integration: settings (bound from the <c>Upvest:</c> section), the HTTP
    /// signer, the token provider, and the signed/authenticated HttpClients backing the API client.
    /// </summary>
    public static IServiceCollection AddUpvestInvestmentApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        services.AddSingleton<IUpvestRequestSigner, UpvestRequestSigner>();
        services.AddSingleton<IUpvestTokenProvider, UpvestTokenProvider>();
        services.AddTransient<UpvestSigningHandler>();
        services.AddTransient<UpvestAuthorizationHandler>();

        // Dedicated client for the token endpoint: signed, but no bearer (avoids a token→token loop).
        services.AddHttpClient(UpvestTokenProvider.HttpClientName, (sp, client) =>
            {
                client.BaseAddress = ResolveBaseAddress(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpvestSettings>>().Value);
            })
            .AddHttpMessageHandler<UpvestSigningHandler>();

        // Main API client: bearer per required scope (outer) then signed (inner, closest to the wire).
        services.AddHttpClient<IUpvestInvestmentApi, UpvestInvestmentApi>(UpvestInvestmentApi.HttpClientName, (sp, client) =>
            {
                client.BaseAddress = ResolveBaseAddress(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpvestSettings>>().Value);
            })
            .AddHttpMessageHandler<UpvestAuthorizationHandler>()
            .AddHttpMessageHandler<UpvestSigningHandler>();

        return services;
    }

    private static Uri ResolveBaseAddress(UpvestSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            throw new InvalidOperationException("Upvest:BaseUrl is not configured.");
        }
        return new Uri(settings.BaseUrl, UriKind.Absolute);
    }
}
