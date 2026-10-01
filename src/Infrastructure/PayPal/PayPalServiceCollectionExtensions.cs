using System;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayPalServerSdk;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Core.Configuration;
using PayPalServerSdk.Servers;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

/// <summary>
/// Wires the PayPal integration: validated options (fail-fast at startup), the SDK client, the gateway/vault/
/// report-reader adapters, and the application services that orchestrate the payment flow.
/// </summary>
public static class PayPalServiceCollectionExtensions
{
    public static IServiceCollection AddPayPalIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        // Fail-fast credential + environment validation: the host refuses to start if a credential is missing or
        // blank, or the environment is not Sandbox — rather than surfacing it as a 401 on the first call.
        services.AddOptions<PayPalOptions>()
            .Bind(configuration.GetSection(PayPalOptions.CONFIG_NAME))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<PayPalOptions>, PayPalOptionsValidator>();

        // Keep the SDK client's default HttpClient pool from caching a DNS answer for the process lifetime.
        services.AddHttpClient(Options.DefaultName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddPayPalServerSdkClient(options =>
        {
            var paypal = configuration.GetSection(PayPalOptions.CONFIG_NAME).Get<PayPalOptions>() ?? new PayPalOptions();

            options.Environment = ServerEnvironment.Sandbox;
            options.Oauth2 = new OAuth2ClientCredentials
            {
                ClientId = paypal.ClientId,
                ClientSecret = paypal.ClientSecret
            };

            // Optional override used verbatim for EVERY call (there is one server group, so this also covers the
            // OAuth2 token endpoint).
            if (!string.IsNullOrWhiteSpace(paypal.BaseUrl))
            {
                options.Server.Default.Sandbox.BaseUrl = paypal.BaseUrl;
            }

            // Card data must never be logged. LoggerFactory is filled from DI by the extension, which also disables
            // the PAYPALSERVERSDKCLIENT_LOG env var from switching body logging on from outside the code.
            options.Logging = options.Logging with
            {
                LogRequestBody = false,
                LogRequestHeaders = false,
                LogResponseHeaders = false
            };

            // Per-attempt timeout (the whole-call budget is enforced by the gateway's linked CancellationToken).
            options.Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) };
        });

        services.AddSingleton<ICurrencyProvider, PayPalCurrencyProvider>();
        services.AddScoped<IPaymentGateway, PayPalGateway>();
        services.AddScoped<ICardVault, PayPalCardVault>();
        services.AddScoped<ITransactionReportReader, PayPalTransactionReportReader>();

        // Application services + the in-process operation lock.
        services.AddSingleton<IOperationLock, KeyedOperationLock>();
        services.AddScoped<IApiOrderService, ApiOrderService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ISavedCardService, SavedCardService>();
        services.AddScoped<IReconciliationService, ReconciliationService>();

        return services;
    }
}
