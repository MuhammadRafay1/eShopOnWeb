using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.eShopWeb.Infrastructure.Upvest.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Upvest integration: settings binding, request signing, authenticated +
    /// signed HTTP clients, and the managed-portfolio orchestration service.
    /// </summary>
    public static IServiceCollection AddUpvestIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        services.AddSingleton<UpvestRequestSigner>();
        services.AddTransient<UpvestSigningHandler>();
        services.AddTransient<UpvestAuthHandler>();
        services.AddSingleton<UpvestTokenProvider>();

        // Auth client: signs the OAuth2 token request (no bearer token yet).
        services.AddHttpClient(UpvestTokenProvider.AuthHttpClientName, (sp, client) =>
            {
                client.BaseAddress = ResolveBaseAddress(sp);
            })
            .AddHttpMessageHandler<UpvestSigningHandler>();

        // API client: adds the bearer token, then signs the request.
        services.AddHttpClient<IUpvestClient, UpvestClient>((sp, client) =>
            {
                client.BaseAddress = ResolveBaseAddress(sp);
            })
            .AddHttpMessageHandler<UpvestAuthHandler>()
            .AddHttpMessageHandler<UpvestSigningHandler>();

        services.AddScoped<IManagedPortfolioService, ManagedPortfolioService>();

        return services;
    }

    private static Uri ResolveBaseAddress(IServiceProvider sp)
    {
        var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpvestSettings>>().Value;
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            throw new InvalidOperationException(
                "Upvest:BaseUrl is not configured. Provide the Upvest settings via user-secrets or configuration.");

        var baseUrl = settings.BaseUrl.EndsWith('/') ? settings.BaseUrl : settings.BaseUrl + "/";
        return new Uri(baseUrl, UriKind.Absolute);
    }
}
