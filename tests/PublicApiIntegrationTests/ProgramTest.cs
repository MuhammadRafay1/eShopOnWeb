using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PublicApiIntegrationTests.Fakes;
using System.Linq;
using System.Net.Http;

namespace PublicApiIntegrationTests;

[TestClass]
public class ProgramTest
{
    private static WebApplicationFactory<Program> _application = new();

    public static HttpClient NewClient
    {
        get
        {
            return _application.CreateClient();
        }
    }

    public static System.IServiceProvider Services => _application.Services;

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext _)
    {
        _application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // No PayPal sandbox calls in this suite: swap the real gateway for a deterministic fake.
            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(d => d.ServiceType == typeof(IPayPalPaymentGateway));
                services.Remove(descriptor);
                // Singleton: the fake tracks authorized amounts across requests (separate scopes) so a
                // later fulfil/capture call in the same test can look up what an earlier pay call authorized.
                services.AddSingleton<IPayPalPaymentGateway, FakePayPalPaymentGateway>();
            });
        });
    }
}
