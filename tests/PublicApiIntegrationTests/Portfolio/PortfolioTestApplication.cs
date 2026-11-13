using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace PublicApiIntegrationTests.Portfolio;

/// <summary>
/// Hosts PublicApi with an isolated in-memory store (unique per instance) and the Upvest boundary replaced
/// by <see cref="FakeUpvestPortfolioService"/>, so the real signed Upvest client is never built.
/// </summary>
public class PortfolioTestApplication : WebApplicationFactory<Program>
{
    public FakeUpvestPortfolioService Upvest { get; } = new();

    private readonly string _dbName = "Portfolio-" + Guid.NewGuid();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddScoped(sp => new DbContextOptionsBuilder<CatalogContext>()
                .UseInMemoryDatabase(_dbName)
                .UseApplicationServiceProvider(sp)
                .Options);
            services.AddScoped(sp => new DbContextOptionsBuilder<AppIdentityDbContext>()
                .UseInMemoryDatabase(_dbName + "-identity")
                .UseApplicationServiceProvider(sp)
                .Options);

            services.RemoveAll<IUpvestPortfolioService>();
            services.AddSingleton<IUpvestPortfolioService>(Upvest);
        });

        return base.CreateHost(builder);
    }
}
