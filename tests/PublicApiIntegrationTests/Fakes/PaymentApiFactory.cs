using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PublicApiIntegrationTests.Fakes;

/// <summary>
/// A WebApplicationFactory that swaps the real PayPal gateway for <see cref="FakePayPalGateway"/> so payment
/// endpoint tests run without touching the network. Uses the same shared in-memory DB the app configures
/// under test (UseOnlyInMemoryDatabase=true in appsettings.test.json).
/// </summary>
public class PaymentApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPayPalPaymentGateway>();
            services.AddSingleton<IPayPalPaymentGateway, FakePayPalGateway>();
        });
    }
}
