using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.PayPal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.PublicApi;

/// <summary>
/// Wires up the PayPal integration (options, HTTP client chain, gateway, and the payment/
/// saved-card/reconciliation application services) for PublicApi.
/// </summary>
public static class PayPalServiceExtensions
{
    public static IServiceCollection AddPayPalIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PayPalOptions>(configuration.GetSection(PayPalOptions.CONFIG_SECTION));
        services.AddSingleton<PayPalProcessInstance>();
        services.AddSingleton<OrderPaymentLock>();

        // Dedicated HttpClient for the token endpoint, with no auth handler attached (it *is*
        // what produces the token, so attaching the handler here would recurse into itself).
        services.AddHttpClient<IPayPalTokenProvider, PayPalTokenProvider>();

        services.AddTransient<PayPalAuthHandler>();
        services.AddHttpClient<PayPalApiClient>()
            .AddHttpMessageHandler<PayPalAuthHandler>();

        services.AddScoped<IPayPalGateway, PayPalGateway>();

        services.AddScoped<IPaymentOrderService>(sp => new PaymentOrderService(
            sp.GetRequiredService<IRepository<Order>>(),
            sp.GetRequiredService<IRepository<Payment>>(),
            sp.GetRequiredService<IRepository<CatalogItem>>(),
            sp.GetRequiredService<IUriComposer>(),
            sp.GetRequiredService<IOptions<PayPalOptions>>().Value.Currency,
            sp.GetRequiredService<PayPalProcessInstance>().InstanceId));

        services.AddScoped<IPaymentService>(sp => new PaymentService(
            sp.GetRequiredService<IRepository<Payment>>(),
            sp.GetRequiredService<IRepository<Order>>(),
            sp.GetRequiredService<IRepository<SavedCard>>(),
            sp.GetRequiredService<IPayPalGateway>(),
            sp.GetRequiredService<OrderPaymentLock>(),
            sp.GetRequiredService<PayPalProcessInstance>().InstanceId));

        services.AddScoped<ISavedCardService>(sp => new SavedCardService(
            sp.GetRequiredService<IRepository<SavedCard>>(),
            sp.GetRequiredService<IPayPalGateway>(),
            sp.GetRequiredService<PayPalProcessInstance>().InstanceId));

        services.AddScoped<IReconciliationService, ReconciliationService>();

        return services;
    }
}
