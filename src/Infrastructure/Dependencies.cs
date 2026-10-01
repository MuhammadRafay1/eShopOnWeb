using System;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Services.Payments;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Infrastructure.PayPal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayPalServerSdk;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Servers;

namespace Microsoft.eShopWeb.Infrastructure;

public static class Dependencies
{
    // Shared across both in-memory DbContexts in this app: without an explicit shared root, EF Core's
    // InMemory provider can build separate internal service-provider caches per DbContext type, which
    // has been observed to make an Add-then-Update sequence on one context fail to surface its update
    // to a later, freshly-scoped read of the same named database. A shared root avoids that entirely.
    private static readonly InMemoryDatabaseRoot InMemoryRoot = new();

    public static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        bool useOnlyInMemoryDatabase = false;
        if (configuration["UseOnlyInMemoryDatabase"] != null)
        {
            useOnlyInMemoryDatabase = bool.Parse(configuration["UseOnlyInMemoryDatabase"]!);
        }

        if (useOnlyInMemoryDatabase)
        {
            services.AddDbContext<CatalogContext>(c =>
               c.UseInMemoryDatabase("Catalog", InMemoryRoot));

            services.AddDbContext<AppIdentityDbContext>(options =>
                options.UseInMemoryDatabase("Identity", InMemoryRoot));
        }
        else
        {
            // use real database
            // Requires LocalDB which can be installed with SQL Server Express 2016
            // https://www.microsoft.com/en-us/download/details.aspx?id=54284
            services.AddDbContext<CatalogContext>(c =>
                c.UseSqlServer(configuration.GetConnectionString("CatalogConnection")));

            // Add Identity DbContext
            services.AddDbContext<AppIdentityDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("IdentityConnection")));
        }
    }

    private const string PayPalHttpClientName = "PayPalServerSdkClient";

    public static void ConfigurePayPalServices(IConfiguration configuration, IServiceCollection services)
    {
        services
            .AddOptions<PayPalSettings>()
            .Bind(configuration.GetSection(PayPalSettings.CONFIG_NAME))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IPaymentSettings>(sp => sp.GetRequiredService<IOptions<PayPalSettings>>().Value);

        services.AddHttpClient(PayPalHttpClientName, c =>
            {
                c.Timeout = TimeSpan.FromSeconds(10); // per-attempt bound; see dotnet-configuration-resilience
            })
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
                Oauth2 = new OAuth2ClientCredentials
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret
                },
                Logging = new PayPalServerSdk.Core.Configuration.LoggingOptions
                {
                    LoggerFactory = sp.GetRequiredService<ILoggerFactory>(),
                    LogRequestBody = false
                }
            };

            if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            {
                options.Server.Default.Sandbox.BaseUrl = settings.BaseUrl;
            }

            return new PayPalServerSdkClient(httpClient, options);
        });

        services.AddScoped<IPayPalPaymentGateway, Microsoft.eShopWeb.Infrastructure.PayPal.PayPalPaymentGateway>();
        services.AddScoped<IOrderPaymentService, OrderPaymentService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();
        services.AddScoped<IReconciliationService, ReconciliationService>();
    }
}
