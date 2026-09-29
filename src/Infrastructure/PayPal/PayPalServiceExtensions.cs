using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

public static class PayPalServiceExtensions
{
    /// <summary>
    /// Binds <see cref="PayPalOptions"/> from the <c>PayPal:</c> configuration section and registers
    /// the token provider and typed PayPal client (with its base address resolved from config).
    /// </summary>
    public static IServiceCollection AddPayPal(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PayPalOptions>(configuration.GetSection(PayPalOptions.ConfigSection));

        // Token provider is a singleton and uses IHttpClientFactory rather than a captured client.
        services.AddHttpClient(PayPalTokenProvider.HttpClientName);
        services.AddSingleton<IPayPalTokenProvider, PayPalTokenProvider>();

        services.AddHttpClient<IPayPalClient, PayPalClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;
            client.BaseAddress = new Uri(PayPalUrlResolver.Resolve(options));
        });

        return services;
    }
}
