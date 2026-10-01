using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.PaymentEndpoints;

[TestClass]
public class OrderPaymentEndpointsTest
{
    private const string OrderBody =
        "{\"items\":[{\"catalogItemId\":5,\"quantity\":2},{\"catalogItemId\":4,\"quantity\":1}]," +
        "\"shipToAddress\":{\"street\":\"1 Market St\",\"city\":\"San Jose\",\"state\":\"CA\",\"country\":\"US\",\"zipCode\":\"95131\"}}";

    private const string CardBody =
        "{\"card\":{\"name\":\"Test\",\"number\":\"4111111111111111\",\"expiry\":\"2030-01\",\"securityCode\":\"123\"," +
        "\"billingAddress\":{\"countryCode\":\"US\"}}}";

    private static HttpClient Client(PayPalApiFactory factory, string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement;
    }

    [TestMethod]
    public async Task HappyPath_Create_Pay_Fulfil_Refund()
    {
        using var factory = new PayPalApiFactory();
        var shopper = Client(factory, ApiTokenHelper.GetNormalUserToken());
        var admin = Client(factory, ApiTokenHelper.GetAdminUserToken());

        var created = await shopper.PostAsync("api/orders", Json(OrderBody));
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var orderId = (await Body(created)).GetProperty("orderId").GetInt32();

        var paid = await shopper.PostAsync($"api/orders/{orderId}/pay", Json(CardBody));
        Assert.AreEqual(HttpStatusCode.OK, paid.StatusCode);
        Assert.AreEqual("Authorized", (await Body(paid)).GetProperty("status").GetString());

        var fulfilled = await admin.PostAsync($"api/orders/{orderId}/fulfil", null);
        Assert.AreEqual(HttpStatusCode.OK, fulfilled.StatusCode);
        var fBody = await Body(fulfilled);
        Assert.AreEqual("Fulfilled", fBody.GetProperty("status").GetString());
        var payment = fBody.GetProperty("payment");
        Assert.AreEqual(1.24m, payment.GetProperty("payPalFeeAmount").GetDecimal());
        Assert.AreEqual(27.76m, payment.GetProperty("netAmount").GetDecimal());

        var refunded = await admin.PostAsync($"api/orders/{orderId}/refunds", Json("{\"amount\":5.00,\"idempotencyKey\":\"k1\"}"));
        Assert.AreEqual(HttpStatusCode.Created, refunded.StatusCode);
        Assert.AreEqual("REF-STUB", (await Body(refunded)).GetProperty("refundId").GetString());
    }

    [TestMethod]
    public async Task Refund_SameIdempotencyKey_DoesNotRefundTwice()
    {
        using var factory = new PayPalApiFactory();
        var shopper = Client(factory, ApiTokenHelper.GetNormalUserToken());
        var admin = Client(factory, ApiTokenHelper.GetAdminUserToken());

        var orderId = (await Body(await shopper.PostAsync("api/orders", Json(OrderBody)))).GetProperty("orderId").GetInt32();
        await shopper.PostAsync($"api/orders/{orderId}/pay", Json(CardBody));
        await admin.PostAsync($"api/orders/{orderId}/fulfil", null);

        var first = await admin.PostAsync($"api/orders/{orderId}/refunds", Json("{\"amount\":5.00,\"idempotencyKey\":\"same-key\"}"));
        var second = await admin.PostAsync($"api/orders/{orderId}/refunds", Json("{\"amount\":5.00,\"idempotencyKey\":\"same-key\"}"));

        var id1 = (await Body(first)).GetProperty("refundId").GetString();
        var id2 = (await Body(second)).GetProperty("refundId").GetString();
        Assert.AreEqual(id1, id2);
    }

    [TestMethod]
    public async Task Refund_BeyondCaptured_IsRejected()
    {
        using var factory = new PayPalApiFactory();
        var shopper = Client(factory, ApiTokenHelper.GetNormalUserToken());
        var admin = Client(factory, ApiTokenHelper.GetAdminUserToken());

        var orderId = (await Body(await shopper.PostAsync("api/orders", Json(OrderBody)))).GetProperty("orderId").GetInt32();
        await shopper.PostAsync($"api/orders/{orderId}/pay", Json(CardBody));
        await admin.PostAsync($"api/orders/{orderId}/fulfil", null);

        var over = await admin.PostAsync($"api/orders/{orderId}/refunds", Json("{\"amount\":1000.00,\"idempotencyKey\":\"big\"}"));
        Assert.AreEqual(HttpStatusCode.BadRequest, over.StatusCode);
    }

    [TestMethod]
    public async Task Fulfil_ByNormalUser_IsForbidden()
    {
        using var factory = new PayPalApiFactory();
        var shopper = Client(factory, ApiTokenHelper.GetNormalUserToken());
        var orderId = (await Body(await shopper.PostAsync("api/orders", Json(OrderBody)))).GetProperty("orderId").GetInt32();

        var response = await shopper.PostAsync($"api/orders/{orderId}/fulfil", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task MyOrders_Anonymous_IsUnauthorized()
    {
        using var factory = new PayPalApiFactory();
        var anon = Client(factory, null);
        var response = await anon.GetAsync("api/my-orders");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Pay_OrderOwnedByAnotherBuyer_IsNotFound()
    {
        using var factory = new PayPalApiFactory();
        var shopper = Client(factory, ApiTokenHelper.GetNormalUserToken());
        var otherIdentity = Client(factory, ApiTokenHelper.GetAdminUserToken()); // a different identity

        var orderId = (await Body(await shopper.PostAsync("api/orders", Json(OrderBody)))).GetProperty("orderId").GetInt32();

        var response = await otherIdentity.PostAsync($"api/orders/{orderId}/pay", Json(CardBody));
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task SavedCard_Save_Reuse_Delete()
    {
        using var factory = new PayPalApiFactory();
        var shopper = Client(factory, ApiTokenHelper.GetNormalUserToken());
        var admin = Client(factory, ApiTokenHelper.GetAdminUserToken());

        var saved = await shopper.PostAsync("api/payment-methods", Json(CardBody));
        Assert.AreEqual(HttpStatusCode.Created, saved.StatusCode);
        var pmId = (await Body(saved)).GetProperty("paymentMethodId").GetInt32();

        // Pay a new order with the saved card.
        var orderId = (await Body(await shopper.PostAsync("api/orders", Json(OrderBody)))).GetProperty("orderId").GetInt32();
        var paid = await shopper.PostAsync($"api/orders/{orderId}/pay", Json($"{{\"paymentMethodId\":{pmId}}}"));
        Assert.AreEqual(HttpStatusCode.OK, paid.StatusCode);
        Assert.AreEqual("Authorized", (await Body(paid)).GetProperty("status").GetString());

        // Delete and confirm it can no longer be used.
        var deleted = await shopper.DeleteAsync($"api/payment-methods/{pmId}");
        Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);

        var orderId2 = (await Body(await shopper.PostAsync("api/orders", Json(OrderBody)))).GetProperty("orderId").GetInt32();
        var payDeleted = await shopper.PostAsync($"api/orders/{orderId2}/pay", Json($"{{\"paymentMethodId\":{pmId}}}"));
        Assert.AreEqual(HttpStatusCode.NotFound, payDeleted.StatusCode);
    }

    [TestMethod]
    public async Task PaymentMethods_AreScopedToCaller()
    {
        using var factory = new PayPalApiFactory();
        var shopper = Client(factory, ApiTokenHelper.GetNormalUserToken());
        var other = Client(factory, ApiTokenHelper.GetAdminUserToken());

        await shopper.PostAsync("api/payment-methods", Json(CardBody));

        var otherList = await other.GetAsync("api/payment-methods");
        var arr = await Body(otherList);
        Assert.AreEqual(0, arr.GetArrayLength());
    }

    [TestMethod]
    public async Task Reconciliation_ByAdmin_ReturnsReport()
    {
        using var factory = new PayPalApiFactory();
        var admin = Client(factory, ApiTokenHelper.GetAdminUserToken());

        var response = await admin.GetAsync("api/reconciliation?from=2026-09-01T00:00:00Z&to=2026-09-15T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        Assert.IsTrue(body.TryGetProperty("inPayPalNotInEShop", out _));
    }

    [TestMethod]
    public async Task Reconciliation_ByNormalUser_IsForbidden()
    {
        using var factory = new PayPalApiFactory();
        var shopper = Client(factory, ApiTokenHelper.GetNormalUserToken());

        var response = await shopper.GetAsync("api/reconciliation?from=2026-09-01T00:00:00Z&to=2026-09-15T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
