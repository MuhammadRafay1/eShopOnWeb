using System;
using System.Net.Http;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayPalServerSdk;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Core.Configuration;
using PayPalServerSdk.Servers;

namespace Microsoft.eShopWeb.Infrastructure.PayPal;

public static class PayPalServiceCollectionExtensions
{
    private const string HttpClientName = "PayPal";

    /// <summary>
    /// Registers a long-lived <see cref="PayPalServerSdkClient"/> over an isolated named
    /// <see cref="HttpClient"/> and wires the <see cref="IPayPalGateway"/>. The caller binds and validates
    /// <see cref="PayPalOptions"/> (with <c>ValidateOnStart</c>) so the host refuses to start when a
    /// credential is missing/blank — rather than discovering it as a 401 on the first call.
    /// </summary>
    public static IServiceCollection AddPayPalIntegration(this IServiceCollection services)
    {
        // Isolated, long-lived HttpClient. PooledConnectionLifetime keeps DNS fresh behind the singleton
        // client; Timeout is a per-attempt backstop below the per-call cancellation budget in the gateway.
        services.AddHttpClient(HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(100))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        services.AddSingleton(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<PayPalOptions>>().Value;
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);

            var sdkOptions = new PayPalServerSdkClientOptions
            {
                Environment = ServerEnvironment.Sandbox,
                Oauth2 = new OAuth2ClientCredentials
                {
                    ClientId = opt.ClientId,
                    ClientSecret = opt.ClientSecret
                },
                // Per-attempt timeout; the whole-call budget is the CancellationToken the gateway passes.
                Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(15) },
                // LoggerFactory is always assigned explicitly so the PAYPALSERVERSDKCLIENT_LOG environment
                // variable can never silently switch body logging on. Request bodies carry PAN/CVV — never log them.
                Logging = new LoggingOptions
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false,
                    LogResponseHeaders = false,
                    LogRequestHeaders = false
                }
            };

            // Optional base-URL override, applied verbatim to the sandbox node — redirects the OAuth2
            // token call as well, since it resolves through the same Default server group.
            if (!string.IsNullOrWhiteSpace(opt.BaseUrl))
            {
                sdkOptions.Server.Default.Sandbox.BaseUrl = opt.BaseUrl!.Trim();
            }

            return new PayPalServerSdkClient(httpClient, sdkOptions);
        });

        services.AddScoped<IPayPalGateway, PayPalGateway>();
        services.AddScoped<IPaymentService, Services.PaymentService>();

        return services;
    }
}
