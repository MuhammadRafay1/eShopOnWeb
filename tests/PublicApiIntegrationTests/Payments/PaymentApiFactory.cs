using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PublicApiIntegrationTests.Payments;

/// <summary>
/// Boots the real PublicApi host but swaps the three PayPal gateways for in-memory fakes, so the
/// payment flows are exercisable end-to-end through the API without live PayPal calls.
/// </summary>
public class PaymentApiFactory : WebApplicationFactory<Program>
{
    public FakeVaultGateway Vault { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPayPalPaymentGateway>();
            services.RemoveAll<IPayPalVaultGateway>();
            services.RemoveAll<IPayPalReconciliationGateway>();

            services.AddScoped<IPayPalPaymentGateway, FakePaymentGateway>();
            services.AddSingleton<IPayPalVaultGateway>(Vault);
            services.AddScoped<IPayPalReconciliationGateway, FakeReconciliationGateway>();
        });
    }
}
