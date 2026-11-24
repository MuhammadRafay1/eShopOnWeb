using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

public static class UpvestServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Upvest gateway: settings bound from the <c>Upvest:</c> section, the signing key,
    /// the token provider, and a named <see cref="System.Net.Http.HttpClient"/> whose base address is
    /// <c>Upvest:BaseUrl</c> and which signs every request.
    /// </summary>
    public static IServiceCollection AddUpvest(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestSettings>(configuration.GetSection(UpvestSettings.SectionName));

        // Bind the base address if configured. Services are constructed lazily (the signing key is
        // loaded only on the first real Upvest call), so a host without Upvest configuration still
        // starts; only an actual portfolio call would then fail.
        var baseUrl = configuration[$"{UpvestSettings.SectionName}:BaseUrl"];

        services.AddSingleton<UpvestSigner>();
        services.AddTransient<UpvestSigningHandler>();
        services.AddSingleton<UpvestTokenProvider>();

        services.AddHttpClient(UpvestHttpClient.Name, client =>
        {
            if (!string.IsNullOrWhiteSpace(baseUrl))
                client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        })
        .AddHttpMessageHandler<UpvestSigningHandler>();

        services.AddScoped<IUpvestClient, UpvestClient>();

        return services;
    }
}
