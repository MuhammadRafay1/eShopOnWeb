using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.OrderEndpoints;

/// <summary>
/// Authorization and ownership behaviour that does not require a live PayPal call — these are
/// rejected (or served) before any PayPal interaction.
/// </summary>
[TestClass]
public class OrderAuthorizationTests
{
    private static HttpClient Client => ProgramTest.NewClient;

    private static HttpClient Authed(string token)
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [TestMethod]
    public async Task Fulfil_WithoutToken_Returns401()
    {
        var response = await Client.PostAsync("api/orders/1/fulfil", null);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Fulfil_AsShopper_Returns403()
    {
        var client = Authed(ApiTokenHelper.GetNormalUserToken());
        var response = await client.PostAsync("api/orders/1/fulfil", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_AsShopper_Returns403()
    {
        var client = Authed(ApiTokenHelper.GetNormalUserToken());
        var response = await client.PostAsync("api/orders/1/cancel", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Reconciliation_AsShopper_Returns403()
    {
        var client = Authed(ApiTokenHelper.GetNormalUserToken());
        var response = await client.GetAsync(
            "api/reconciliation?from=2026-09-01T00:00:00Z&to=2026-09-15T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task MyOrders_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("api/my-orders");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Fulfil_UnknownOrder_AsAdmin_Returns404()
    {
        var client = Authed(ApiTokenHelper.GetAdminUserToken());
        var response = await client.PostAsync("api/orders/999999/fulfil", null);
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task CreateOrder_ThenListedInMyOrders()
    {
        var client = Authed(ApiTokenHelper.GetNormalUserToken());
        var create = await client.PostAsJsonAsync("api/orders", new
        {
            items = new[] { new { catalogItemId = 1, quantity = 2 } },
            shipToAddress = new { street = "1 Market", city = "SF", state = "CA", country = "US", zipCode = "94105" }
        });
        Assert.AreEqual(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CreatedOrder>();
        Assert.IsNotNull(created);
        Assert.IsTrue(created!.OrderId > 0);
        Assert.AreEqual("AwaitingPayment", created.Status);

        var mine = await client.GetAsync("api/my-orders");
        mine.EnsureSuccessStatusCode();
        var body = await mine.Content.ReadAsStringAsync();
        StringAssert.Contains(body, $"\"orderId\":{created.OrderId}");
    }

    private class CreatedOrder
    {
        public int OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }
}
