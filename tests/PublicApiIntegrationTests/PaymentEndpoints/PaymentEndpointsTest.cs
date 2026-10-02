using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.PaymentEndpoints;

[TestClass]
public class PaymentEndpointsTest
{
    private static HttpClient ClientFor(string? token = null)
    {
        var client = ProgramTest.NewClient;
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    [TestMethod]
    public async Task PlaceOrder_Unauthenticated_Returns401()
    {
        var client = ClientFor();
        var response = await client.PostAsync("api/orders", Json(new { items = new[] { new { catalogItemId = 1, quantity = 1 } } }));
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task PlaceOrder_NormalUser_PlacesOrderAndAppearsInMyOrders()
    {
        var client = ClientFor(ApiTokenHelper.GetNormalUserToken());

        // Use a catalog id that actually exists in the seeded in-memory catalog.
        var catalog = await client.GetAsync("api/catalog-items?pageSize=1&pageIndex=0");
        catalog.EnsureSuccessStatusCode();
        using var catalogDoc = JsonDocument.Parse(await catalog.Content.ReadAsStringAsync());
        var catalogItemId = catalogDoc.RootElement.GetProperty("catalogItems")[0].GetProperty("id").GetInt32();

        var place = await client.PostAsync("api/orders", Json(new { items = new[] { new { catalogItemId, quantity = 2 } } }));
        Assert.AreEqual(HttpStatusCode.Created, place.StatusCode);

        using var placed = JsonDocument.Parse(await place.Content.ReadAsStringAsync());
        var orderId = placed.RootElement.GetProperty("orderId").GetInt32();
        Assert.IsTrue(orderId > 0);

        var mine = await client.GetAsync("api/my-orders");
        mine.EnsureSuccessStatusCode();
        using var orders = JsonDocument.Parse(await mine.Content.ReadAsStringAsync());
        var arr = orders.RootElement.GetProperty("orders");
        var found = false;
        foreach (var o in arr.EnumerateArray())
        {
            if (o.GetProperty("orderId").GetInt32() == orderId)
            {
                found = true;
                Assert.AreEqual("AwaitingPayment", o.GetProperty("paymentStatus").GetString());
            }
        }
        Assert.IsTrue(found, "placed order should appear in my-orders awaiting payment");
    }

    [TestMethod]
    public async Task Fulfil_NormalUser_Returns403()
    {
        var client = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var response = await client.PostAsync("api/orders/1/fulfil", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_NormalUser_Returns403()
    {
        var client = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var response = await client.PostAsync("api/orders/1/cancel", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Reconciliation_NormalUser_Returns403()
    {
        var client = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var response = await client.GetAsync("api/reconciliation?from=2020-01-01T00:00:00Z&to=2020-01-02T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PaymentMethods_Unauthenticated_Returns401()
    {
        var client = ClientFor();
        var response = await client.GetAsync("api/payment-methods");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task PaymentMethods_NormalUser_StartsEmpty()
    {
        var client = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var response = await client.GetAsync("api/payment-methods");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.IsTrue(doc.RootElement.TryGetProperty("paymentMethods", out _));
    }
}
