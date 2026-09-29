using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.Infrastructure.Payments.PayPal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;

namespace Microsoft.eShopWeb.PublicApi.Configuration;

public static class PayPalServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PayPal integration (options binding, the single shared HttpClient with a
    /// transient-fault retry policy, the token provider and the three gateways). Registered only
    /// here in PublicApi so the Web host never picks up an outbound PayPal dependency.
    /// </summary>
    public static IServiceCollection AddPayPalIntegration(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PayPalOptions>(configuration.GetSection(PayPalOptions.SectionName));
        services.AddMemoryCache();

        // One HttpClient for every PayPal call - token endpoint included - with the base address
        // resolved from options (honoring the PayPal:BaseUrl override verbatim when set).
        services.AddHttpClient(PayPalHttpClientNames.PayPal, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;
            client.BaseAddress = new Uri(options.ResolveBaseUrl() + "/");
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        // Retries only transient faults (network errors, 5xx, 408) with exponential backoff.
        // 4xx business responses (declines, AUTHORIZATION_EXPIRED, ...) are deliberately not retried.
        .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(3,
            attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt))));

        services.AddSingleton<IPayPalAccessTokenProvider, PayPalAccessTokenProvider>();
        services.AddScoped<PayPalApiClient>();
        services.AddScoped<IPayPalPaymentGateway, PayPalOrdersGateway>();
        services.AddScoped<IPayPalVaultGateway, PayPalVaultGateway>();
        services.AddScoped<IPayPalReconciliationGateway, PayPalReconciliationGateway>();

        return services;
    }
}
