using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PublicApiIntegrationTests;

/// <summary>
/// A PublicApi test host with the PayPal gateway replaced by <see cref="FakePaymentGatewayService"/>
/// and an in-memory catalog store isolated to this factory instance (a unique database name), so
/// tests do not share orders/payments. One host per instance keeps its store across the requests of
/// a single create → pay → fulfil → refund flow.
/// </summary>
public class PaymentApiFactory : WebApplicationFactory<Program>
{
    public FakePaymentGatewayService Gateway { get; } = new();
    private readonly string _catalogDbName = "Catalog-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPaymentGatewayService>();
            services.AddSingleton<IPaymentGatewayService>(Gateway);

            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CatalogContext>));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddDbContext<CatalogContext>(c => c.UseInMemoryDatabase(_catalogDbName));
        });
    }
}
