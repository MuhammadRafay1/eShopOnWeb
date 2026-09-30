using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Infrastructure.PayPal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayPalServerSdk;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Servers;

namespace Microsoft.eShopWeb.Infrastructure;

public static class Dependencies
{
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
               c.UseInMemoryDatabase("Catalog"));

            services.AddDbContext<AppIdentityDbContext>(options =>
                options.UseInMemoryDatabase("Identity"));
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

        ConfigurePayPal(configuration, services);
    }

    private static void ConfigurePayPal(IConfiguration configuration, IServiceCollection services)
    {
        services.Configure<PayPalSettings>(configuration.GetSection("PayPal"));
        services.AddSingleton<IPaymentGatewaySettings, PayPalGatewaySettings>();

        var paypalSection = configuration.GetSection("PayPal");
        var clientId = paypalSection["ClientId"] ?? string.Empty;
        var clientSecret = paypalSection["ClientSecret"] ?? string.Empty;
        var baseUrl = paypalSection["BaseUrl"];

        services.AddPayPalServerSdkClient(options =>
        {
            // This SDK build exposes only ServerEnvironment.Sandbox (no Live member); targeting live
            // PayPal, if ever needed, is done via the BaseUrl override below, not via Environment.
            options.Environment = ServerEnvironment.Sandbox;
            options.Oauth2 = new OAuth2ClientCredentials
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            };

            if (!string.IsNullOrEmpty(baseUrl))
            {
                // Applies verbatim to every PayPal call, including the OAuth token request itself.
                options.Server.Default.Sandbox.BaseUrl = baseUrl;
            }
        });

        services.AddScoped<IPaymentGateway, PayPalPaymentGateway>();
    }
}
