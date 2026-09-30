using System;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayPalServerSdk;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Servers;

namespace Microsoft.eShopWeb.Infrastructure.Payments;

public static class PayPalIntegrationExtensions
{
    private const string PayPalHttpClientName = "PayPal";

    public static IServiceCollection AddPayPalIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PayPalOptions>(configuration.GetSection("PayPal"));

        services.AddTransient<PayPalWireLoggingHandler>();
        services.AddHttpClient(PayPalHttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
            .AddHttpMessageHandler<PayPalWireLoggingHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;

            var clientOptions = new PayPalServerSdkClientOptions
            {
                Environment = ServerEnvironment.Sandbox,
                Oauth2 = new OAuth2ClientCredentials
                {
                    ClientId = options.ClientId,
                    ClientSecret = options.ClientSecret
                }
            };

            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                clientOptions.Server = new ServerOptions
                {
                    Default = new DefaultOptions
                    {
                        Sandbox = new DefaultOptions.SandboxOptions { BaseUrl = options.BaseUrl }
                    }
                };
            }

            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(PayPalHttpClientName);
            return new PayPalServerSdkClient(httpClient, clientOptions);
        });

        services.AddSingleton<ICurrencyProvider, CurrencyProvider>();
        services.AddScoped<IPaymentGateway, PayPalPaymentGateway>();

        return services;
    }
}
