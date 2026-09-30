using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

public static class PayPalServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PayPal gateway, the payment/order application services, and binds
    /// <see cref="PayPalOptions"/> from the "PayPal" configuration section. Configures the gateway's
    /// HttpClient base address from PayPal:BaseUrl (verbatim) or, failing that, PayPal:Environment.
    /// </summary>
    public static IServiceCollection AddPayPalIntegration(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PayPalOptions>(configuration.GetSection(PayPalOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PayPalOptions>>().Value);

        var options = new PayPalOptions();
        configuration.GetSection(PayPalOptions.SectionName).Bind(options);
        var baseAddress = ResolveBaseUrl(options);

        services.AddHttpClient<IPayPalGateway, PayPalGateway>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();

        return services;
    }

    /// <summary>
    /// Resolves the PayPal API base address. When PayPal:BaseUrl is set it is used verbatim for
    /// every call (including the OAuth token request); otherwise it is derived from the environment.
    /// An unrecognised environment with no BaseUrl override fails fast rather than guessing.
    /// </summary>
    public static string ResolveBaseUrl(PayPalOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return options.BaseUrl.TrimEnd('/') + "/";
        }

        return (options.Environment ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "sandbox" => "https://api-m.sandbox.paypal.com/",
            "live" or "production" => "https://api-m.paypal.com/",
            _ => throw new InvalidOperationException(
                $"Unrecognised PayPal:Environment '{options.Environment}'. Set PayPal:Environment to " +
                "'sandbox' or 'live', or provide an explicit PayPal:BaseUrl.")
        };
    }
}
