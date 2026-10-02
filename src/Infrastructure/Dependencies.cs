using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Infrastructure.PayPal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PayPalServerSdk;
using PayPalServerSdk.Core.Authentication.OAuth2.ClientCredentials;
using PayPalServerSdk.Core.Configuration;
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
        services.AddOptions<PayPalOptions>()
            .Bind(configuration.GetSection(PayPalOptions.ConfigSectionName))
            .ValidateDataAnnotations()
            .Validate(o => string.Equals(o.Environment, "sandbox", System.StringComparison.OrdinalIgnoreCase),
                "PayPal:Environment must be 'sandbox' — this integration targets the PayPal sandbox only.")
            .ValidateOnStart();

        var section = configuration.GetSection(PayPalOptions.ConfigSectionName);
        string? clientId = section["ClientId"];
        string? clientSecret = section["ClientSecret"];
        string? baseUrl = section["BaseUrl"];

        services.AddPayPalServerSdkClient(options =>
        {
            options.Oauth2 = new OAuth2ClientCredentials
            {
                ClientId = clientId ?? string.Empty,
                ClientSecret = clientSecret ?? string.Empty
            };
            options.Environment = ServerEnvironment.Sandbox;
            options.Retry = RetryOptions.Default() with { Timeout = System.TimeSpan.FromSeconds(15) };

            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                options.Server.Default.Sandbox.BaseUrl = baseUrl;
            }
        });

        services.AddScoped<IPayPalPaymentGateway, PayPalPaymentGateway>();
    }
}
