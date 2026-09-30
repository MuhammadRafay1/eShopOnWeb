using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

public static class PayPalDependencies
{
    public static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        services.AddOptions<PayPalOptions>()
            .Bind(configuration.GetSection(PayPalOptions.CONFIG_SECTION));

        services.AddMemoryCache();

        services.AddHttpClient<PayPalClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<PayPalOptions>>().Value;
            client.BaseAddress = new Uri(options.ResolveBaseUrl());
        });

        services.AddScoped<IPayPalPaymentGateway, PayPalPaymentGateway>();
    }
}
