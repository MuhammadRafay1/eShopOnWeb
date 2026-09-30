using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PublicApiIntegrationTests.Fakes;

namespace PublicApiIntegrationTests.PaymentEndpoints;

[TestClass]
public class PaymentFlowEndpointTests
{
    private static PaymentApiFactory _factory = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => _factory = new PaymentApiFactory();

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    private HttpClient Client(string? token)
    {
        var client = _factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private const string CardJson =
        "{\"number\":\"4111111111111111\",\"expiryMonth\":12,\"expiryYear\":2030,\"securityCode\":\"123\",\"cardholderName\":\"T\",\"billingAddress\":{\"line1\":\"1 St\",\"city\":\"C\",\"state\":\"WA\",\"postalCode\":\"98052\",\"countryCode\":\"US\"}}";

    private static string OrderJson(int itemId = 1, int qty = 1) =>
        $"{{\"shippingAddress\":{{\"street\":\"1 St\",\"city\":\"C\",\"state\":\"WA\",\"country\":\"USA\",\"zipCode\":\"98052\"}},\"items\":[{{\"catalogItemId\":{itemId},\"quantity\":{qty}}}]}}";

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage r)
    {
        var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private async Task<int> CreateOrderAsync(string token, int itemId = 1, int qty = 1)
    {
        var r = await Client(token).PostAsync("api/orders", Json(OrderJson(itemId, qty)));
        r.EnsureSuccessStatusCode();
        return (await BodyAsync(r)).GetProperty("orderId").GetInt32();
    }

    // ---- auth / role enforcement ----

    [TestMethod]
    public async Task MyOrders_WithoutToken_Returns401()
    {
        var r = await Client(null).GetAsync("api/my-orders");
        Assert.AreEqual(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [TestMethod]
    public async Task Fulfil_AsShopper_Returns403()
    {
        var r = await Client(ApiTokenHelper.GetNormalUserToken()).PostAsync("api/orders/1/fulfil", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_AsShopper_Returns403()
    {
        var r = await Client(ApiTokenHelper.GetNormalUserToken()).PostAsync("api/orders/1/cancel", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [TestMethod]
    public async Task Reconciliation_AsShopper_Returns403()
    {
        var r = await Client(ApiTokenHelper.GetNormalUserToken())
            .GetAsync("api/reconciliation?from=2026-01-01T00:00:00Z&to=2026-01-31T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ---- happy flow: create -> pay -> fulfil -> refund ----

    [TestMethod]
    public async Task FullFlow_CreatePayFulfilRefund()
    {
        var shopper = ApiTokenHelper.GetNormalUserToken();
        var admin = ApiTokenHelper.GetAdminUserToken();
        var orderId = await CreateOrderAsync(shopper);

        var pay = await Client(shopper).PostAsync($"api/orders/{orderId}/pay", Json($"{{\"card\":{CardJson}}}"));
        pay.EnsureSuccessStatusCode();
        Assert.AreEqual("Authorized", (await BodyAsync(pay)).GetProperty("status").GetString());

        var fulfil = await Client(admin).PostAsync($"api/orders/{orderId}/fulfil", null);
        fulfil.EnsureSuccessStatusCode();
        var fb = await BodyAsync(fulfil);
        Assert.AreEqual("Fulfilled", fb.GetProperty("status").GetString());
        Assert.IsTrue(fb.GetProperty("capturedAmount").GetDecimal() > 0);

        var refund = await Client(shopper).PostAsync($"api/orders/{orderId}/refunds", Json("{\"amount\":5.00,\"idempotencyKey\":\"k1\"}"));
        Assert.AreEqual(HttpStatusCode.Created, refund.StatusCode);
        var rb = await BodyAsync(refund);
        Assert.AreEqual(5.00m, rb.GetProperty("amount").GetDecimal());
        var refundId = rb.GetProperty("refundId").GetInt32();

        // idempotent replay -> same refundId, no second refund
        var replay = await Client(shopper).PostAsync($"api/orders/{orderId}/refunds", Json("{\"amount\":5.00,\"idempotencyKey\":\"k1\"}"));
        Assert.AreEqual(refundId, (await BodyAsync(replay)).GetProperty("refundId").GetInt32());
    }

    [TestMethod]
    public async Task OverRefund_Returns400()
    {
        var shopper = ApiTokenHelper.GetNormalUserToken();
        var admin = ApiTokenHelper.GetAdminUserToken();
        var orderId = await CreateOrderAsync(shopper);
        await Client(shopper).PostAsync($"api/orders/{orderId}/pay", Json($"{{\"card\":{CardJson}}}"));
        await Client(admin).PostAsync($"api/orders/{orderId}/fulfil", null);

        var r = await Client(shopper).PostAsync($"api/orders/{orderId}/refunds", Json("{\"amount\":99999.00,\"idempotencyKey\":\"big\"}"));
        Assert.AreEqual(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [TestMethod]
    public async Task Pay_BothCardAndSavedCard_Returns400()
    {
        var shopper = ApiTokenHelper.GetNormalUserToken();
        var orderId = await CreateOrderAsync(shopper);
        var r = await Client(shopper).PostAsync($"api/orders/{orderId}/pay",
            Json($"{{\"paymentMethodId\":1,\"card\":{CardJson}}}"));
        Assert.AreEqual(HttpStatusCode.BadRequest, r.StatusCode);
    }

    // ---- ownership isolation ----

    [TestMethod]
    public async Task Pay_AnotherShoppersOrder_Returns404()
    {
        var orderId = await CreateOrderAsync(ApiTokenHelper.GetNormalUserToken());
        // admin acts as a different shopper identity
        var r = await Client(ApiTokenHelper.GetAdminUserToken())
            .PostAsync($"api/orders/{orderId}/pay", Json($"{{\"card\":{CardJson}}}"));
        Assert.AreEqual(HttpStatusCode.NotFound, r.StatusCode);
    }

    // ---- saved cards ----

    [TestMethod]
    public async Task SavedCard_Save_List_PayWith_Delete_ThenUnusable()
    {
        var shopper = ApiTokenHelper.GetNormalUserToken();

        var save = await Client(shopper).PostAsync("api/payment-methods", Json($"{{\"card\":{CardJson}}}"));
        Assert.AreEqual(HttpStatusCode.Created, save.StatusCode);
        var sb = await BodyAsync(save);
        var pmId = sb.GetProperty("paymentMethodId").GetInt32();
        Assert.AreEqual("VISA", sb.GetProperty("brand").GetString());

        var list = await BodyAsync(await Client(shopper).GetAsync("api/payment-methods"));
        Assert.IsTrue(list.GetProperty("paymentMethods").GetArrayLength() >= 1);

        var orderId = await CreateOrderAsync(shopper, itemId: 2);
        var pay = await Client(shopper).PostAsync($"api/orders/{orderId}/pay", Json($"{{\"paymentMethodId\":{pmId}}}"));
        pay.EnsureSuccessStatusCode();
        Assert.AreEqual("Authorized", (await BodyAsync(pay)).GetProperty("status").GetString());

        var del = await Client(shopper).DeleteAsync($"api/payment-methods/{pmId}");
        Assert.AreEqual(HttpStatusCode.NoContent, del.StatusCode);

        var order2 = await CreateOrderAsync(shopper, itemId: 2);
        var payDeleted = await Client(shopper).PostAsync($"api/orders/{order2}/pay", Json($"{{\"paymentMethodId\":{pmId}}}"));
        Assert.AreEqual(HttpStatusCode.NotFound, payDeleted.StatusCode);
    }

    [TestMethod]
    public async Task DeleteAnotherShoppersCard_Returns404()
    {
        // demouser saves a card
        var save = await Client(ApiTokenHelper.GetNormalUserToken()).PostAsync("api/payment-methods", Json($"{{\"card\":{CardJson}}}"));
        var pmId = (await BodyAsync(save)).GetProperty("paymentMethodId").GetInt32();

        // admin (different identity) attempts to delete it
        var del = await Client(ApiTokenHelper.GetAdminUserToken()).DeleteAsync($"api/payment-methods/{pmId}");
        Assert.AreEqual(HttpStatusCode.NotFound, del.StatusCode);
    }
}
