using System;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Portfolio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Registers the Upvest-backed managed-portfolio services: options, signing, token acquisition,
/// the typed Upvest client, the local store and the application service.
/// </summary>
public static class UpvestServiceCollectionExtensions
{
    public const string HttpClientName = "upvest";

    public static IServiceCollection AddUpvestPortfolio(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpvestOptions>(configuration.GetSection(UpvestOptions.SectionName));

        // A named HttpClient whose base address is the configured Upvest base URL (used verbatim).
        services.AddHttpClient(HttpClientName, (sp, http) =>
        {
            // Set the base address when configured. When it is absent the host still starts and DI
            // validation passes; portfolio requests then fail with a clear error (see UpvestClient).
            var options = sp.GetRequiredService<IOptions<UpvestOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                http.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        });

        services.AddSingleton<UpvestMessageSigner>();
        services.AddSingleton<UpvestTokenProvider>(sp => new UpvestTokenProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
            sp.GetRequiredService<UpvestMessageSigner>(),
            sp.GetRequiredService<IOptions<UpvestOptions>>()));

        services.AddSingleton<IPortfolioStore, InMemoryPortfolioStore>();

        services.AddScoped<IUpvestClient>(sp => new UpvestClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
            sp.GetRequiredService<UpvestMessageSigner>(),
            sp.GetRequiredService<UpvestTokenProvider>()));

        services.AddScoped<IPortfolioService, PortfolioService>();
        return services;
    }
}
