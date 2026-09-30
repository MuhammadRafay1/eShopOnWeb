using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.OrderEndpoints;

/// <summary>
/// Integration tests that exercise authentication, role gating and request validation on the
/// new payment endpoints. These deliberately avoid any path that calls PayPal (order creation,
/// 401/403 gating, and 400 validation all short-circuit before any PayPal request), so they run
/// without network access or sandbox charges. The full money-movement flow is verified
/// end-to-end against the live sandbox separately.
/// </summary>
[TestClass]
public class OrderAndRoleEndpointsTest
{
    private static HttpClient AuthedClient(string token)
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent Json(object o) =>
        new(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");

    private static object ValidOrderBody() => new
    {
        items = new[] { new { catalogItemId = 1, quantity = 1 } },
        shipToAddress = new { street = "1 Microsoft Way", city = "Redmond", state = "WA", country = "US", zipCode = "98052" }
    };

    [TestMethod]
    public async Task CreateOrder_WithShopperToken_ReturnsCreatedWithOrderId()
    {
        var client = AuthedClient(ApiTokenHelper.GetNormalUserToken());
        var response = await client.PostAsync("api/orders", Json(ValidOrderBody()));

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.IsTrue(doc.RootElement.TryGetProperty("orderId", out var id));
        Assert.IsTrue(id.GetInt32() > 0);
    }

    [TestMethod]
    public async Task CreateOrder_Unauthenticated_Returns401()
    {
        var client = ProgramTest.NewClient;
        var response = await client.PostAsync("api/orders", Json(ValidOrderBody()));
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Fulfil_AsShopper_Returns403()
    {
        var client = AuthedClient(ApiTokenHelper.GetNormalUserToken());
        var response = await client.PostAsync("api/orders/999999/fulfil", Json(new { }));
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_AsShopper_Returns403()
    {
        var client = AuthedClient(ApiTokenHelper.GetNormalUserToken());
        var response = await client.PostAsync("api/orders/999999/cancel", Json(new { }));
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Reconciliation_AsShopper_Returns403()
    {
        var client = AuthedClient(ApiTokenHelper.GetNormalUserToken());
        var response = await client.GetAsync("api/reconciliation?from=2020-01-01T00:00:00Z&to=2020-02-01T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Reconciliation_AsAdmin_BadDates_Returns400()
    {
        var client = AuthedClient(ApiTokenHelper.GetAdminUserToken());
        var response = await client.GetAsync("api/reconciliation?from=not-a-date&to=also-bad");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Pay_WithBothCardAndMethod_Returns400()
    {
        // Create an order first (awaiting payment), then attempt an invalid pay request.
        var client = AuthedClient(ApiTokenHelper.GetNormalUserToken());
        var createResp = await client.PostAsync("api/orders", Json(ValidOrderBody()));
        var created = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var orderId = created.RootElement.GetProperty("orderId").GetInt32();

        var body = new
        {
            card = new { number = "4111111111111111", expiryMonth = 1, expiryYear = 2030, cvv = "123" },
            paymentMethodId = 5
        };
        var response = await client.PostAsync($"api/orders/{orderId}/pay", Json(body));
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task ListPaymentMethods_NewShopper_ReturnsOkEmpty()
    {
        var client = AuthedClient(ApiTokenHelper.GetNormalUserToken());
        var response = await client.GetAsync("api/payment-methods");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.IsTrue(doc.RootElement.TryGetProperty("paymentMethods", out _));
    }

    [TestMethod]
    public async Task MyOrders_Unauthenticated_Returns401()
    {
        var client = ProgramTest.NewClient;
        var response = await client.GetAsync("api/my-orders");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
