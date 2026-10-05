using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MaxioAdvancedBilling;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.PublicApi.Billing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.SubscriptionEndpoints;

/// <summary>
/// Exercises the Maxio gateway against <see cref="FakeMaxio"/> through the real SDK client — no network.
/// </summary>
[TestClass]
public class MaxioBillingGatewayTests
{
    private static MaxioBillingGateway CreateGateway(FakeMaxio fake, double budgetSeconds = 10, double attemptSeconds = 5)
    {
        var settings = new MaxioSettings
        {
            ApiKey = "offline-test-placeholder",
            Subdomain = "offline-test",
            ProductFamilyHandle = fake.ProductFamilyHandle,
            BaseUrl = FakeMaxio.BaseUrl,
            RequestBudgetSeconds = budgetSeconds,
            AttemptTimeoutSeconds = attemptSeconds,
            PlanCacheSeconds = 0,
        };
        var client = new MaxioAdvancedBillingClient(new HttpClient(fake),
            MaxioServiceCollectionExtensions.BuildClientOptions(settings, NullLoggerFactory.Instance, TimeProvider.System));
        return new MaxioBillingGateway(client, Options.Create(settings), new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System, NullLogger<MaxioBillingGateway>.Instance);
    }

    private static NewBillingCustomer NewCustomer(string reference = "eshop-ref-1") =>
        new(reference, "shopper@example.test", "Shop", "Per");

    [TestMethod]
    public async Task ListPlans_uses_family_handle_and_skips_archived_products()
    {
        var fake = new FakeMaxio();
        using var gateway = CreateGateway(fake);

        var catalog = await gateway.ListPlansAsync();

        CollectionAssert.AreEqual(new[] { "eshop-pro", "basic-plan" }, catalog.Plans.Select(p => p.Handle).ToArray());
        Assert.AreEqual(29900, catalog.Find("eshop-pro")!.PriceInCents);
        Assert.AreEqual("month", catalog.Find("eshop-pro")!.IntervalUnit);
        Assert.IsFalse(catalog.IsTruncated);
        var request = fake.Requests.Single();
        StringAssert.Contains(request.Path, "/product_families/handle");
        StringAssert.Contains(request.Path, "test-family/products.json");
        StringAssert.Contains(request.Query, "per_page=200");
    }

    [TestMethod]
    public async Task ListPlans_reports_truncation_when_the_page_cap_is_hit()
    {
        var fake = new FakeMaxio();
        var fullPage = new JsonArray(Enumerable.Range(1, 200)
            .Select(i => (JsonNode)new JsonObject { ["product"] = FakeMaxio.Product(i, $"plan-{i}", $"Plan {i}", 100) }).ToArray());
        fake.Interceptor = (req, _) => req.RequestUri!.AbsolutePath.EndsWith("/products.json")
            ? FakeMaxio.Json(HttpStatusCode.OK, fullPage.DeepClone())
            : null;
        using var gateway = CreateGateway(fake);

        var catalog = await gateway.ListPlansAsync();

        Assert.IsTrue(catalog.IsTruncated);
        Assert.AreEqual(5, fake.Requests.Count, "the page loop must stop at its cap");
    }

    [TestMethod]
    public async Task ListPlans_unknown_family_is_a_configuration_error()
    {
        var fake = new FakeMaxio("missing-family");
        fake.Interceptor = (_, _) => FakeMaxio.Json(HttpStatusCode.NotFound, JsonValue.Create("Not found"));
        using var gateway = CreateGateway(fake);

        var ex = await Assert.ThrowsExceptionAsync<BillingProviderException>(() => gateway.ListPlansAsync());

        Assert.AreEqual(BillingFailureKind.Misconfigured, ex.Kind);
    }

    [TestMethod]
    public async Task Unauthorized_is_a_configuration_error_without_leaking_details()
    {
        var fake = new FakeMaxio();
        fake.Interceptor = (_, _) => FakeMaxio.RawJson(HttpStatusCode.Unauthorized, "{}");
        using var gateway = CreateGateway(fake);

        var ex = await Assert.ThrowsExceptionAsync<BillingProviderException>(() => gateway.ListPlansAsync());

        Assert.AreEqual(BillingFailureKind.Misconfigured, ex.Kind);
        Assert.AreEqual(401, ex.ProviderStatusCode);
        Assert.IsFalse(ex.Message.Contains("http", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Malformed_success_body_is_a_provider_error()
    {
        var fake = new FakeMaxio();
        fake.Interceptor = (_, _) => FakeMaxio.RawJson(HttpStatusCode.OK, "{\"not\":\"a list\"}");
        using var gateway = CreateGateway(fake);

        var ex = await Assert.ThrowsExceptionAsync<BillingProviderException>(() => gateway.ListPlansAsync());

        Assert.AreEqual(BillingFailureKind.ProviderError, ex.Kind);
    }

    [TestMethod]
    public async Task FindCustomerByReference_returns_null_on_404()
    {
        var fake = new FakeMaxio();
        using var gateway = CreateGateway(fake);

        Assert.IsNull(await gateway.FindCustomerIdByReferenceAsync("eshop-unknown"));
    }

    [TestMethod]
    public async Task CreateCustomer_sends_reference_and_returns_id()
    {
        var fake = new FakeMaxio();
        using var gateway = CreateGateway(fake);

        var id = await gateway.CreateCustomerAsync(NewCustomer());

        Assert.AreEqual(fake.Customers.Keys.Single(), id);
        var body = FakeMaxio.Parse(fake.Requests.Single().Body).RootElement.GetProperty("customer");
        Assert.AreEqual("eshop-ref-1", body.GetProperty("reference").GetString());
        Assert.AreEqual("shopper@example.test", body.GetProperty("email").GetString());
    }

    [TestMethod]
    public async Task CreateCustomer_connection_failure_settles_by_reference_lookup()
    {
        var fake = new FakeMaxio();
        // The create reaches Maxio and is applied, but the connection drops before the answer arrives.
        fake.Interceptor = (req, body) =>
        {
            if (req.Method != HttpMethod.Post) return null;
            fake.Customers[42] = new JsonObject { ["id"] = 42, ["reference"] = "eshop-ref-1" };
            throw new HttpRequestException("connection reset");
        };
        using var gateway = CreateGateway(fake);

        var id = await gateway.CreateCustomerAsync(NewCustomer());

        Assert.AreEqual(42, id);
        Assert.AreEqual(1, fake.Count(HttpMethod.Post, "/customers.json"), "a POST must never be resent");
        Assert.AreEqual(1, fake.Count(HttpMethod.Get, "/customers/lookup.json"));
    }

    [TestMethod]
    public async Task CreateCustomer_duplicate_reference_returns_existing_customer()
    {
        var fake = new FakeMaxio();
        fake.Customers[77] = new JsonObject { ["id"] = 77, ["reference"] = "eshop-ref-1" };
        using var gateway = CreateGateway(fake);

        Assert.AreEqual(77, await gateway.CreateCustomerAsync(NewCustomer()));
    }

    [TestMethod]
    public async Task CreateSubscription_sends_plan_customer_reference_and_collection_method()
    {
        var fake = new FakeMaxio();
        fake.Customers[1] = new JsonObject { ["id"] = 1, ["reference"] = "c" };
        using var gateway = CreateGateway(fake);

        var subscription = await gateway.CreateSubscriptionAsync(1, "eshop-pro", "eshop-sub-abc");

        Assert.AreEqual("active", subscription.State);
        Assert.AreEqual("eshop-pro", subscription.PlanHandle);
        Assert.AreEqual(29900, subscription.PriceInCents);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), subscription.NextBillingAt);
        var body = FakeMaxio.Parse(fake.Requests.Single().Body).RootElement.GetProperty("subscription");
        Assert.AreEqual("eshop-pro", body.GetProperty("product_handle").GetString());
        Assert.AreEqual(1, body.GetProperty("customer_id").GetInt32());
        Assert.AreEqual("eshop-sub-abc", body.GetProperty("reference").GetString());
        Assert.AreEqual("remittance", body.GetProperty("payment_collection_method").GetString());
    }

    [TestMethod]
    public async Task CreateSubscription_connection_failure_settles_by_reference_lookup()
    {
        var fake = new FakeMaxio();
        var customer = new JsonObject { ["id"] = 1, ["reference"] = "c" };
        fake.Customers[1] = customer;
        fake.Interceptor = (req, _) =>
        {
            if (req.Method != HttpMethod.Post) return null;
            fake.Subscriptions[900] = (1, FakeMaxio.Subscription(900, "eshop-sub-abc", fake.Products[0], customer));
            throw new HttpRequestException("connection reset");
        };
        using var gateway = CreateGateway(fake);

        var subscription = await gateway.CreateSubscriptionAsync(1, "eshop-pro", "eshop-sub-abc");

        Assert.AreEqual(900, subscription.Id);
        Assert.AreEqual(1, fake.Count(HttpMethod.Post, "/subscriptions.json"), "a POST must never be resent");
        Assert.AreEqual(1, fake.Count(HttpMethod.Get, "/customers/1/subscriptions.json"));
    }

    [TestMethod]
    public async Task CreateSubscription_unconfirmable_outcome_is_reported_as_unknown()
    {
        var fake = new FakeMaxio();
        fake.Customers[1] = new JsonObject { ["id"] = 1, ["reference"] = "c" };
        fake.Interceptor = (req, _) => req.Method == HttpMethod.Post ? throw new HttpRequestException("connection reset") : null;
        using var gateway = CreateGateway(fake);

        var ex = await Assert.ThrowsExceptionAsync<BillingOutcomeUnknownException>(
            () => gateway.CreateSubscriptionAsync(1, "eshop-pro", "eshop-sub-abc"));

        StringAssert.Contains(ex.Message, "safe to retry");
    }

    [TestMethod]
    public async Task CreateSubscription_rejection_carries_provider_messages_and_is_not_reconciled()
    {
        var fake = new FakeMaxio();
        fake.Interceptor = (req, _) => req.Method == HttpMethod.Post
            ? FakeMaxio.Json(HttpStatusCode.UnprocessableEntity, new JsonObject { ["errors"] = new JsonArray("No payment method was on file") })
            : null;
        using var gateway = CreateGateway(fake);

        var ex = await Assert.ThrowsExceptionAsync<BillingProviderException>(
            () => gateway.CreateSubscriptionAsync(1, "eshop-pro", "eshop-sub-abc"));

        Assert.AreEqual(BillingFailureKind.Rejected, ex.Kind);
        CollectionAssert.Contains(ex.ProviderMessages.ToList(), "No payment method was on file");
        Assert.AreEqual(1, fake.Requests.Count);
    }

    [TestMethod]
    public async Task Hung_provider_is_cut_off_at_the_request_budget()
    {
        var fake = new FakeMaxio { Hang = true };
        using var gateway = CreateGateway(fake, budgetSeconds: 1, attemptSeconds: 1);
        var stopwatch = Stopwatch.StartNew();

        var ex = await Assert.ThrowsExceptionAsync<BillingProviderException>(() => gateway.ListPlansAsync());

        Assert.AreEqual(BillingFailureKind.Timeout, ex.Kind);
        StringAssert.Contains(ex.Message, "Maxio did not respond");
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"took {stopwatch.Elapsed}");
    }
}
