using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PublicApiIntegrationTests.Payments;

/// <summary>
/// A WebApplicationFactory that swaps the real PayPal client for <see cref="FakePayPalClient"/> so
/// the payment endpoints can be driven end-to-end without any network access. Each instance gets
/// its own in-memory store, keeping tests isolated.
/// </summary>
public class PaymentApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPayPalClient>();
            // Singleton so the fake's amount tracking persists across requests within one factory
            // (an authorize in one request, a capture in the next).
            services.AddSingleton<IPayPalClient, FakePayPalClient>();
        });
    }
}
