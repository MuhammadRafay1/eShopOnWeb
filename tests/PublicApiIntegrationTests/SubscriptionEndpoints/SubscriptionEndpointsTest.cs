using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.eShopWeb.PublicApi.Billing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.SubscriptionEndpoints;

/// <summary>
/// End-to-end through the PublicApi HTTP pipeline (JWT auth, endpoints, EF in-memory claims, SDK) with Maxio
/// replaced by <see cref="FakeMaxio"/> at the HttpClient handler — no network.
/// </summary>
[TestClass]
public class SubscriptionEndpointsTest
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static (WebApplicationFactory<Program> Factory, HttpClient Client) CreateApi(FakeMaxio fake, string userName,
        double budgetSeconds = 10)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Maxio:RequestBudgetSeconds", budgetSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Maxio:AttemptTimeoutSeconds", Math.Min(5, budgetSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.ConfigureTestServices(services =>
                services.AddHttpClient(MaxioServiceCollectionExtensions.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => fake));
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetUserToken(userName));
        return (factory, client);
    }

    private static string UniqueUser() => $"subscriber-{Guid.NewGuid():N}@example.test";

    private static StringContent Subscribe(string planHandle) =>
        new(JsonSerializer.Serialize(new { planHandle }), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [TestMethod]
    public async Task Endpoints_require_a_bearer_token()
    {
        var (factory, _) = CreateApi(new FakeMaxio(), UniqueUser());
        using var _f = factory;
        var anonymous = factory.CreateClient();

        Assert.AreEqual(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("api/subscription-plans")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("api/my-subscriptions")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("api/subscriptions", Subscribe("eshop-pro"))).StatusCode);
    }

    [TestMethod]
    public async Task Lists_plans_of_the_configured_family()
    {
        var (factory, client) = CreateApi(new FakeMaxio(), UniqueUser());
        using var _f = factory;

        var response = await client.GetAsync("api/subscription-plans");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJson(response);
        Assert.AreEqual("test-family", body.GetProperty("productFamilyHandle").GetString());
        Assert.IsFalse(body.GetProperty("truncated").GetBoolean());
        var plans = body.GetProperty("plans").EnumerateArray().ToList();
        CollectionAssert.AreEqual(new[] { "eshop-pro", "basic-plan" }, plans.Select(p => p.GetProperty("handle").GetString()).ToArray());
        Assert.AreEqual(299m, plans[0].GetProperty("price").GetDecimal());
    }

    [TestMethod]
    public async Task Subscribe_creates_customer_and_subscription_then_is_idempotent()
    {
        var fake = new FakeMaxio();
        var user = UniqueUser();
        var (factory, client) = CreateApi(fake, user);
        using var _f = factory;

        var first = await client.PostAsync("api/subscriptions", Subscribe("eshop-pro"));
        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode, await first.Content.ReadAsStringAsync());
        var created = await ReadJson(first);
        Assert.IsTrue(created.GetProperty("created").GetBoolean());
        var subscription = created.GetProperty("subscription");
        Assert.AreEqual("eshop-pro", subscription.GetProperty("planHandle").GetString());
        Assert.AreEqual("active", subscription.GetProperty("state").GetString());
        Assert.AreEqual(299m, subscription.GetProperty("price").GetDecimal());
        Assert.AreEqual(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), subscription.GetProperty("nextBillingAt").GetDateTimeOffset());

        var second = await client.PostAsync("api/subscriptions", Subscribe("eshop-pro"));
        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode);
        var repeated = await ReadJson(second);
        Assert.IsFalse(repeated.GetProperty("created").GetBoolean());
        Assert.AreEqual(subscription.GetProperty("id").GetInt32(), repeated.GetProperty("subscription").GetProperty("id").GetInt32());

        Assert.AreEqual(1, fake.Count(HttpMethod.Post, "/customers.json"));
        Assert.AreEqual(1, fake.Count(HttpMethod.Post, "/subscriptions.json"));
        var customer = fake.Customers.Values.Single();
        Assert.AreEqual(user, (string?)customer["email"]);
        Assert.IsFalse(((string?)customer["reference"])!.Contains('@'), "the customer reference must not carry the e-mail");

        var mine = await ReadJson(await client.GetAsync("api/my-subscriptions"));
        Assert.AreEqual(subscription.GetProperty("id").GetInt32(),
            mine.GetProperty("subscriptions").EnumerateArray().Single().GetProperty("id").GetInt32());
    }

    [TestMethod]
    public async Task Concurrent_double_click_creates_exactly_one_subscription()
    {
        var fake = new FakeMaxio { CreateLatency = TimeSpan.FromMilliseconds(300) };
        var (factory, client) = CreateApi(fake, UniqueUser());
        using var _f = factory;

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.PostAsync("api/subscriptions", Subscribe("eshop-pro"))));

        Assert.AreEqual(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.IsTrue(responses.All(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict or HttpStatusCode.OK),
            string.Join(",", responses.Select(r => (int)r.StatusCode)));
        Assert.AreEqual(1, fake.Count(HttpMethod.Post, "/customers.json"));
        Assert.AreEqual(1, fake.Count(HttpMethod.Post, "/subscriptions.json"));
    }

    [TestMethod]
    public async Task Existing_provider_subscription_is_adopted_instead_of_duplicated()
    {
        // Simulates a lost local store (in-memory DB restart): Maxio already holds the customer and the subscription.
        var fake = new FakeMaxio();
        var user = UniqueUser();
        var reference = Microsoft.eShopWeb.ApplicationCore.Services.SubscriptionService.CustomerReferenceFor(user);
        var customer = new JsonObject { ["id"] = 31, ["reference"] = reference, ["email"] = user };
        fake.Customers[31] = customer;
        fake.Subscriptions[600] = (31, FakeMaxio.Subscription(600, "eshop-sub-old", fake.Products[0], customer));
        var (factory, client) = CreateApi(fake, user);
        using var _f = factory;

        var response = await client.PostAsync("api/subscriptions", Subscribe("eshop-pro"));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(600, (await ReadJson(response)).GetProperty("subscription").GetProperty("id").GetInt32());
        Assert.AreEqual(0, fake.Count(HttpMethod.Post, "/customers.json"));
        Assert.AreEqual(0, fake.Count(HttpMethod.Post, "/subscriptions.json"));
    }

    [TestMethod]
    public async Task Unknown_plan_is_rejected_before_any_write()
    {
        var fake = new FakeMaxio();
        var (factory, client) = CreateApi(fake, UniqueUser());
        using var _f = factory;

        var archived = await client.PostAsync("api/subscriptions", Subscribe("old-plan"));
        var unknown = await client.PostAsync("api/subscriptions", Subscribe("not-a-plan"));
        var missing = await client.PostAsync("api/subscriptions", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.AreEqual(HttpStatusCode.BadRequest, archived.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.IsFalse(fake.Requests.Any(r => r.Method == HttpMethod.Post));
    }

    [TestMethod]
    public async Task Provider_rejection_returns_422_and_releases_the_claim()
    {
        var fake = new FakeMaxio();
        var reject = true;
        fake.Interceptor = (req, _) => reject && req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == "/subscriptions.json"
            ? FakeMaxio.Json(HttpStatusCode.UnprocessableEntity, new JsonObject { ["errors"] = new JsonArray("No payment method was on file") })
            : null;
        var (factory, client) = CreateApi(fake, UniqueUser());
        using var _f = factory;

        var rejected = await client.PostAsync("api/subscriptions", Subscribe("eshop-pro"));
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        StringAssert.Contains(await rejected.Content.ReadAsStringAsync(), "No payment method was on file");

        reject = false;
        var retried = await client.PostAsync("api/subscriptions", Subscribe("eshop-pro"));
        Assert.AreEqual(HttpStatusCode.Created, retried.StatusCode, "a refused claim must not block a later attempt");
    }

    [TestMethod]
    public async Task Unknown_write_outcome_is_settled_on_the_next_request()
    {
        var fake = new FakeMaxio();
        var dropConnection = true;
        fake.Interceptor = (req, body) =>
        {
            if (!dropConnection || req.Method != HttpMethod.Post || req.RequestUri!.AbsolutePath != "/subscriptions.json") return null;
            dropConnection = false;
            var reference = (string?)JsonNode.Parse(body!)!["subscription"]!["reference"];
            var customerId = (int)JsonNode.Parse(body!)!["subscription"]!["customer_id"]!;
            // Applied at Maxio, but the reconciliation read fails too, so the outcome stays unknown for now.
            fake.Subscriptions[700] = (customerId, FakeMaxio.Subscription(700, reference, fake.Products[0], fake.Customers[customerId]));
            fake.Interceptor = (r, _) => r.RequestUri!.AbsolutePath.EndsWith("/subscriptions.json") && r.Method == HttpMethod.Get
                ? throw new HttpRequestException("still down")
                : null;
            throw new HttpRequestException("connection reset");
        };
        var (factory, client) = CreateApi(fake, UniqueUser());
        using var _f = factory;

        var first = await client.PostAsync("api/subscriptions", Subscribe("eshop-pro"));
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, first.StatusCode, await first.Content.ReadAsStringAsync());
        StringAssert.Contains(await first.Content.ReadAsStringAsync(), "safe to retry");

        fake.Interceptor = null; // Maxio is healthy again
        var mine = await ReadJson(await client.GetAsync("api/my-subscriptions"));
        Assert.AreEqual(700, mine.GetProperty("subscriptions").EnumerateArray().Single().GetProperty("id").GetInt32());

        var retried = await client.PostAsync("api/subscriptions", Subscribe("eshop-pro"));
        Assert.AreEqual(HttpStatusCode.OK, retried.StatusCode);
        Assert.AreEqual(700, (await ReadJson(retried)).GetProperty("subscription").GetProperty("id").GetInt32());
        Assert.AreEqual(1, fake.Count(HttpMethod.Post, "/subscriptions.json"), "the retry must not create a second subscription");
    }

    [TestMethod]
    public async Task Unresponsive_maxio_returns_504_within_the_budget()
    {
        var fake = new FakeMaxio { Hang = true };
        var (factory, client) = CreateApi(fake, UniqueUser(), budgetSeconds: 1.5);
        using var _f = factory;
        var stopwatch = Stopwatch.StartNew();

        var plans = await client.GetAsync("api/subscription-plans");
        var subscribe = await client.PostAsync("api/subscriptions", Subscribe("eshop-pro"));
        var mine = await client.GetAsync("api/my-subscriptions");

        foreach (var response in new[] { plans, subscribe, mine })
        {
            Assert.AreEqual(HttpStatusCode.GatewayTimeout, response.StatusCode);
            StringAssert.Contains(await response.Content.ReadAsStringAsync(), "Maxio did not respond");
        }
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"took {stopwatch.Elapsed}");
    }
}
