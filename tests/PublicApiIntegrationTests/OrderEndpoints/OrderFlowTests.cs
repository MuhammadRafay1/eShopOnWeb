using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.OrderEndpoints;

[TestClass]
public class OrderFlowTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly PaymentApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient ClientFor(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object OneItemOrder => new { items = new[] { new { catalogItemId = 1, quantity = 2 } } };
    private static object CardPayment => new
    {
        card = new { name = "Test Buyer", number = "4111111111111111", expiry = "2027-12", securityCode = "123" }
    };

    private static JsonElement FindOrder(JsonElement myOrders, int orderId)
    {
        foreach (var o in myOrders.GetProperty("orders").EnumerateArray())
            if (o.GetProperty("orderId").GetInt32() == orderId)
                return o;
        throw new AssertFailedException($"Order {orderId} not found in my-orders.");
    }

    private async Task<(int orderId, decimal total)> CreateOrderAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("api/orders", OneItemOrder, Json);
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        return (body.GetProperty("orderId").GetInt32(), body.GetProperty("total").GetDecimal());
    }

    [TestMethod]
    public async Task CreatePayFulfilRefund_HappyPath()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var admin = ClientFor(ApiTokenHelper.GetAdminUserToken());

        var (orderId, total) = await CreateOrderAsync(shopper);

        // Pay: authorize (hold, not capture).
        var payResp = await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", CardPayment, Json);
        Assert.AreEqual(HttpStatusCode.OK, payResp.StatusCode);
        var pay = await payResp.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.AreEqual("PaymentAuthorized", pay.GetProperty("status").GetString());
        Assert.IsFalse(string.IsNullOrEmpty(pay.GetProperty("payment").GetProperty("authorizationId").GetString()));

        // Fulfil (operator): capture, with fee/net reported.
        var fulfilResp = await admin.PostAsync($"api/orders/{orderId}/fulfil", null);
        Assert.AreEqual(HttpStatusCode.OK, fulfilResp.StatusCode);
        var fulfil = await fulfilResp.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.AreEqual("Fulfilled", fulfil.GetProperty("status").GetString());
        var captured = fulfil.GetProperty("payment");
        Assert.AreEqual(total, captured.GetProperty("capturedAmount").GetDecimal());
        Assert.IsTrue(captured.GetProperty("payPalFee").GetDecimal() > 0);
        Assert.IsTrue(captured.GetProperty("netAmount").GetDecimal() > 0);

        // Partial refund.
        var refundResp = await shopper.PostAsJsonAsync($"api/orders/{orderId}/refunds",
            new { amount = 1.00m, idempotencyKey = "refund-key-1" }, Json);
        Assert.AreEqual(HttpStatusCode.Created, refundResp.StatusCode);
        var refund = await refundResp.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.IsTrue(refund.GetProperty("refundId").GetInt32() > 0);
        Assert.AreEqual(total - 1.00m, refund.GetProperty("remainingRefundable").GetDecimal());

        // my-orders reflects capture + refund.
        var mine = await shopper.GetFromJsonAsync<JsonElement>("api/my-orders", Json);
        var order0 = FindOrder(mine, orderId);
        Assert.AreEqual("PartiallyRefunded", order0.GetProperty("status").GetString());
        Assert.AreEqual(1, order0.GetProperty("payment").GetProperty("refunds").GetArrayLength());
    }

    [TestMethod]
    public async Task Pay_IsIdempotent_OnDoubleClick()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var (orderId, _) = await CreateOrderAsync(shopper);

        var first = await (await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", CardPayment, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var second = await (await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", CardPayment, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.AreEqual(
            first.GetProperty("payment").GetProperty("authorizationId").GetString(),
            second.GetProperty("payment").GetProperty("authorizationId").GetString());
    }

    [TestMethod]
    public async Task Refund_IsIdempotent_UnderSameKey()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var admin = ClientFor(ApiTokenHelper.GetAdminUserToken());
        var (orderId, _) = await CreateOrderAsync(shopper);
        await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", CardPayment, Json);
        await admin.PostAsync($"api/orders/{orderId}/fulfil", null);

        var r1 = await (await shopper.PostAsJsonAsync($"api/orders/{orderId}/refunds",
            new { amount = 2.00m, idempotencyKey = "dup-key" }, Json)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var r2 = await (await shopper.PostAsJsonAsync($"api/orders/{orderId}/refunds",
            new { amount = 2.00m, idempotencyKey = "dup-key" }, Json)).Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.AreEqual(r1.GetProperty("refundId").GetInt32(), r2.GetProperty("refundId").GetInt32());

        var mine = await shopper.GetFromJsonAsync<JsonElement>("api/my-orders", Json);
        Assert.AreEqual(1, FindOrder(mine, orderId).GetProperty("payment").GetProperty("refunds").GetArrayLength());
    }

    [TestMethod]
    public async Task OverRefund_IsRejected()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var admin = ClientFor(ApiTokenHelper.GetAdminUserToken());
        var (orderId, total) = await CreateOrderAsync(shopper);
        await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", CardPayment, Json);
        await admin.PostAsync($"api/orders/{orderId}/fulfil", null);

        var resp = await shopper.PostAsJsonAsync($"api/orders/{orderId}/refunds",
            new { amount = total + 100m, idempotencyKey = "over" }, Json);
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_ReleasesHold_ThenPayIsConflict()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var admin = ClientFor(ApiTokenHelper.GetAdminUserToken());
        var (orderId, _) = await CreateOrderAsync(shopper);
        await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", CardPayment, Json);

        var cancel = await admin.PostAsync($"api/orders/{orderId}/cancel", null);
        Assert.AreEqual(HttpStatusCode.OK, cancel.StatusCode);

        var payAgain = await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", CardPayment, Json);
        Assert.AreEqual(HttpStatusCode.Conflict, payAgain.StatusCode);
    }

    [TestMethod]
    public async Task Fulfil_And_Cancel_RequireAdmin()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var (orderId, _) = await CreateOrderAsync(shopper);
        await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", CardPayment, Json);

        Assert.AreEqual(HttpStatusCode.Forbidden, (await shopper.PostAsync($"api/orders/{orderId}/fulfil", null)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await shopper.PostAsync($"api/orders/{orderId}/cancel", null)).StatusCode);
    }

    [TestMethod]
    public async Task Shopper_CannotActOnAnothersOrder()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var other = ClientFor(ApiTokenHelper.GetAdminUserToken()); // different identity
        var (orderId, _) = await CreateOrderAsync(shopper);

        // Admin is a different buyer; the shopper-scoped pay/refund must not see this order.
        var pay = await other.PostAsJsonAsync($"api/orders/{orderId}/pay", CardPayment, Json);
        Assert.AreEqual(HttpStatusCode.NotFound, pay.StatusCode);
    }

    [TestMethod]
    public async Task PayerActionRequired_ReportsUnprocessable()
    {
        _factory.Gateway.NextAuthorizeRequiresPayerAction = true;
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var (orderId, _) = await CreateOrderAsync(shopper);

        var pay = await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", CardPayment, Json);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, pay.StatusCode);
    }
}
