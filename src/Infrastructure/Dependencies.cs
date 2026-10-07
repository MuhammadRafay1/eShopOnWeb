using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.eShopWeb.Infrastructure;

public static class Dependencies
{
    /// <summary>
    /// Registers the "invest your change" feature: the Upvest client (built once from the
    /// <c>Upvest:</c> settings and reused), the gateway that talks to it, and the application
    /// service. Intended for the PublicApi host only.
    /// </summary>
    public static void AddUpvestInvesting(IConfiguration configuration, IServiceCollection services)
    {
        var settings = new UpvestSettings();
        configuration.GetSection(UpvestSettings.SectionName).Bind(settings);
        services.AddSingleton(settings);

        // One long-lived connection (SDK client + signed HTTP client), built lazily and reused.
        services.AddSingleton(_ => UpvestClientFactory.Create(settings));

        services.AddScoped<IUpvestInvestingGateway, UpvestInvestingGateway>();
        services.AddScoped<IInvestingService, InvestingService>();
    }

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
    }
}
