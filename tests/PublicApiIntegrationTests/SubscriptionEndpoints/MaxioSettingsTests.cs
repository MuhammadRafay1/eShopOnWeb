using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MaxioAdvancedBilling;
using Microsoft.eShopWeb.PublicApi.Billing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.SubscriptionEndpoints;

[TestClass]
public class MaxioSettingsTests
{
    private static MaxioSettings Valid() => new()
    {
        ApiKey = "offline-test-placeholder",
        Subdomain = "offline-test",
        ProductFamilyHandle = "test-family",
    };

    [TestMethod]
    public void Valid_settings_pass()
    {
        Assert.IsTrue(new MaxioSettingsValidator().Validate(null, Valid()).Succeeded);
    }

    [DataTestMethod]
    [DataRow("ApiKey", "Maxio:ApiKey")]
    [DataRow("ProductFamilyHandle", "Maxio:ProductFamilyHandle")]
    [DataRow("Subdomain", "Maxio:Subdomain")]
    public void Blank_required_setting_fails_and_names_the_key(string property, string key)
    {
        var settings = Valid();
        typeof(MaxioSettings).GetProperty(property)!.SetValue(settings, "  ");

        var result = new MaxioSettingsValidator().Validate(null, settings);

        Assert.IsTrue(result.Failed);
        Assert.IsTrue(result.Failures!.Any(f => f.Contains(key)), string.Join(" | ", result.Failures!));
    }

    [TestMethod]
    public void Subdomain_is_optional_when_base_url_is_set()
    {
        var settings = Valid();
        settings.Subdomain = null;
        settings.BaseUrl = "https://billing-proxy.example.test";

        Assert.IsTrue(new MaxioSettingsValidator().Validate(null, settings).Succeeded);
    }

    [DataTestMethod]
    [DataRow(30.0)]
    [DataRow(0.0)]
    public void Request_budget_must_stay_under_thirty_seconds(double seconds)
    {
        var settings = Valid();
        settings.RequestBudgetSeconds = seconds;

        Assert.IsTrue(new MaxioSettingsValidator().Validate(null, settings).Failed);
    }

    [TestMethod]
    public void Unknown_collection_method_fails()
    {
        var settings = Valid();
        settings.PaymentCollectionMethod = "card";

        Assert.IsTrue(new MaxioSettingsValidator().Validate(null, settings).Failed);
    }

    [TestMethod]
    public async Task Base_url_override_is_used_verbatim_and_subdomain_otherwise()
    {
        foreach (var (baseUrl, expectedHost) in new[] { ("https://billing-proxy.example.test/", "billing-proxy.example.test"), (null, "offline-test.chargify.com") })
        {
            var fake = new FakeMaxio();
            var settings = Valid();
            settings.BaseUrl = baseUrl;
            var client = new MaxioAdvancedBillingClient(new HttpClient(fake),
                MaxioServiceCollectionExtensions.BuildClientOptions(settings, NullLoggerFactory.Instance, TimeProvider.System));

            await client.Customers.ReadCustomerByReference(
                new MaxioAdvancedBilling.Requests.Customers.ReadCustomerByReferenceRequest { Reference = "x" })
                .ContinueWith(_ => { }); // the fake answers 404; only the address matters here

            Assert.AreEqual(expectedHost, fake.LastHost);
            Assert.AreEqual("/customers/lookup.json", fake.Requests.Last().Path);
        }
    }
}
