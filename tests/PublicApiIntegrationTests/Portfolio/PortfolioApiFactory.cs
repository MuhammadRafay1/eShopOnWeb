using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PublicApiIntegrationTests.Portfolio;

/// <summary>
/// Boots the PublicApi with the real pipeline but (1) replaces the Upvest client with
/// <see cref="FakeUpvestClient"/> and (2) gives each factory its own isolated in-memory databases,
/// so portfolio endpoints are exercised end-to-end without network access, credentials, or cross-test bleed.
/// </summary>
public class PortfolioApiFactory : WebApplicationFactory<Program>
{
    public FakeUpvestClient Upvest { get; } = new();

    private readonly string _id = Guid.NewGuid().ToString("N");
    private readonly InMemoryDatabaseRoot _catalogRoot = new();
    private readonly InMemoryDatabaseRoot _identityRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUpvestClient>();
            services.AddSingleton<IUpvestClient>(Upvest);

            services.RemoveAll<DbContextOptions<CatalogContext>>();
            services.AddDbContext<CatalogContext>(o => o.UseInMemoryDatabase($"Catalog-{_id}", _catalogRoot));

            services.RemoveAll<DbContextOptions<AppIdentityDbContext>>();
            services.AddDbContext<AppIdentityDbContext>(o => o.UseInMemoryDatabase($"Identity-{_id}", _identityRoot));
        });
    }
}
