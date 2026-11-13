using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi.Configuration;

/// <summary>
/// Registers the Upvest managed-portfolio integration: settings bound from the <c>Upvest</c>
/// configuration section, the signing key provider/signer, token cache, the typed HTTP client, and
/// the high-level <see cref="IUpvestService"/>.
/// </summary>
public static class UpvestServiceCollectionExtensions
{
    public static IServiceCollection AddUpvestIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<UpvestSettings>()
            .Bind(configuration.GetSection(UpvestSettings.SectionName))
            .Validate(s => !string.IsNullOrWhiteSpace(s.BaseUrl), "Upvest:BaseUrl must be configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.ClientId), "Upvest:ClientId must be configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SigningKeyId), "Upvest:SigningKeyId must be configured.")
            .Validate(s => !string.IsNullOrWhiteSpace(s.SigningKeyPath), "Upvest:SigningKeyPath must be configured.");

        services.AddSingleton<UpvestSigningKeyProvider>();
        services.AddSingleton<UpvestRequestSigner>();
        services.AddSingleton<UpvestTokenCache>();

        services.AddHttpClient<UpvestApiClient>((provider, client) =>
        {
            var settings = provider.GetRequiredService<IOptions<UpvestSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(100);
        });

        services.AddScoped<IUpvestService, UpvestService>();

        return services;
    }
}
