using System;
using Microsoft.eShopWeb.ApplicationCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using PayPalServerSdk;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Core.Configuration;
using PayPalServerSdk.Servers;

namespace Microsoft.eShopWeb.Infrastructure;

public static class PayPalIntegrationServiceCollectionExtensions
{
    private const string PayPalHttpClientName = "PayPal";

    public static IServiceCollection AddPayPalIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PayPalSettings>()
            .Bind(configuration.GetSection(PayPalSettings.CONFIG_NAME))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Expose the bound PayPalSettings directly so ApplicationCore services can take it without
        // depending on the Microsoft.Extensions.Options package.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PayPalSettings>>().Value);

        services.AddHttpClient(PayPalHttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<PayPalSettings>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(PayPalHttpClientName);

            var options = new PayPalServerSdkClientOptions
            {
                Environment = ServerEnvironment.Sandbox,
                Oauth2 = new OAuth2ClientCredentials { ClientId = settings.ClientId, ClientSecret = settings.ClientSecret },
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(10) },
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogRequestHeaders = false,
                    LogResponseHeaders = false
                }
            };

            if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                options.Server.Default.Sandbox.BaseUrl = settings.BaseUrl;
            }

            return new PayPalServerSdkClient(httpClient, options);
        });

        services.AddScoped<IPayPalPaymentGateway, PayPalPaymentGateway>();
        services.AddScoped<IPaymentOperationClaimStore, EfPaymentOperationClaimStore>();
        services.AddScoped<IOrderPlacementService, OrderPlacementService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IReconciliationService, ReconciliationService>();

        return services;
    }
}
