using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Registers the PayPal integration: options binding, the OAuth2 token provider, the four typed
/// clients (each with its base address resolved from PayPal:BaseUrl or the environment), and the
/// ApplicationCore orchestration services that drive them.
/// </summary>
public static class PayPalDependencies
{
    public static IServiceCollection AddPayPalIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PayPalOptions>(configuration.GetSection(PayPalOptions.ConfigSection));

        // The single configured currency is exposed to the domain layer through this abstraction.
        services.AddSingleton<IPayPalCurrencyProvider>(sp =>
            sp.GetRequiredService<IOptions<PayPalOptions>>().Value);

        services.AddMemoryCache();

        // OAuth2 token endpoint — same base URL as every other call (honouring PayPal:BaseUrl).
        services.AddHttpClient<IPayPalAccessTokenProvider, PayPalAccessTokenProvider>(ConfigureBaseAddress);

        services.AddHttpClient<IPayPalOrdersClient, PayPalOrdersClient>(ConfigureBaseAddress);
        services.AddHttpClient<IPayPalPaymentsClient, PayPalPaymentsClient>(ConfigureBaseAddress);
        services.AddHttpClient<IPayPalVaultClient, PayPalVaultClient>(ConfigureBaseAddress);
        services.AddHttpClient<IPayPalTransactionSearchClient, PayPalTransactionSearchClient>(ConfigureBaseAddress);

        // ApplicationCore services that orchestrate the flows.
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderPaymentService, OrderPaymentService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IReconciliationService, ReconciliationService>();

        return services;
    }

    private static void ConfigureBaseAddress(IServiceProvider sp, System.Net.Http.HttpClient client)
    {
        var options = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;
        client.BaseAddress = PayPalBaseUrlResolver.ResolveUri(options);
    }
}
