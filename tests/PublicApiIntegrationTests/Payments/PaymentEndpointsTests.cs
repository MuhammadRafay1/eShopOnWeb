using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.Payments;

[TestClass]
public class PaymentEndpointsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private PaymentApiFactory _factory = null!;

    [TestInitialize]
    public void Init() => _factory = new PaymentApiFactory();

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();

    private HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object CardBody() => new
    {
        card = new
        {
            name = "John Doe",
            number = "4111111111111111",
            expiry = "2030-01",
            securityCode = "123",
            billingAddress = new { addressLine1 = "1 Main St", adminArea2 = "San Jose", adminArea1 = "CA", postalCode = "95131", countryCode = "US" }
        }
    };

    private static object OrderBody() => new
    {
        items = new[] { new { catalogItemId = 1, quantity = 1 } }
    };

    private async Task<int> CreateOrderAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("api/orders", OrderBody(), Json);
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("orderId").GetInt32();
    }

    [TestMethod]
    public async Task PlaceOrder_Pay_Fulfil_Refund_HappyPath()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var admin = Client(ApiTokenHelper.GetAdminUserToken());

        var orderId = await CreateOrderAsync(user);

        var pay = await user.PostAsJsonAsync($"api/orders/{orderId}/pay", CardBody(), Json);
        Assert.AreEqual(HttpStatusCode.OK, pay.StatusCode);
        var payDoc = JsonDocument.Parse(await pay.Content.ReadAsStringAsync());
        Assert.AreEqual("PaymentAuthorized", payDoc.RootElement.GetProperty("status").GetString());
        Assert.AreEqual("CREATED", payDoc.RootElement.GetProperty("payment").GetProperty("authorizationStatus").GetString());

        var fulfil = await admin.PostAsync($"api/orders/{orderId}/fulfil", null);
        Assert.AreEqual(HttpStatusCode.OK, fulfil.StatusCode);
        var fulfilDoc = JsonDocument.Parse(await fulfil.Content.ReadAsStringAsync());
        var payment = fulfilDoc.RootElement.GetProperty("payment");
        Assert.AreEqual("Fulfilled", fulfilDoc.RootElement.GetProperty("status").GetString());
        Assert.IsTrue(payment.GetProperty("capturedAmount").GetDecimal() > 0);
        Assert.IsTrue(payment.TryGetProperty("payPalFee", out var fee) && fee.GetDecimal() >= 0);
        Assert.IsTrue(payment.GetProperty("netAmount").GetDecimal() > 0);

        var refundBody = new { amount = 0.01m, idempotencyKey = "refund-key-1" };
        var refund = await admin.PostAsJsonAsync($"api/orders/{orderId}/refunds", refundBody, Json);
        Assert.AreEqual(HttpStatusCode.Created, refund.StatusCode);
        var refundDoc = JsonDocument.Parse(await refund.Content.ReadAsStringAsync());
        Assert.IsTrue(refundDoc.RootElement.GetProperty("refundId").GetInt32() > 0);
        Assert.AreEqual("PartiallyRefunded", refundDoc.RootElement.GetProperty("orderStatus").GetString());
    }

    [TestMethod]
    public async Task Fulfil_AsNormalUser_Returns403()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var orderId = await CreateOrderAsync(user);
        var resp = await user.PostAsync($"api/orders/{orderId}/fulfil", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_AsNormalUser_Returns403()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var orderId = await CreateOrderAsync(user);
        var resp = await user.PostAsync($"api/orders/{orderId}/cancel", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [TestMethod]
    public async Task Reconciliation_AsNormalUser_Returns403()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var resp = await user.GetAsync("api/reconciliation?from=2020-01-01T00:00:00Z&to=2020-01-15T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [TestMethod]
    public async Task Reconciliation_AsAdmin_ReturnsWellFormedReport()
    {
        var admin = Client(ApiTokenHelper.GetAdminUserToken());
        var resp = await admin.GetAsync("api/reconciliation?from=2020-01-01T00:00:00Z&to=2020-01-15T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.IsTrue(doc.RootElement.TryGetProperty("matched", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("onlyInEShop", out _));
        Assert.IsTrue(doc.RootElement.TryGetProperty("onlyInPayPal", out _));
    }

    [TestMethod]
    public async Task Reconciliation_InvalidRange_Returns400()
    {
        var admin = Client(ApiTokenHelper.GetAdminUserToken());
        var resp = await admin.GetAsync("api/reconciliation?from=2020-02-01T00:00:00Z&to=2020-01-01T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [TestMethod]
    public async Task Pay_AnotherUsersOrder_Returns404()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var orderId = await CreateOrderAsync(user);

        // Admin is a different buyer; they must not be able to pay someone else's order.
        var other = Client(ApiTokenHelper.GetAdminUserToken());
        var resp = await other.PostAsJsonAsync($"api/orders/{orderId}/pay", CardBody(), Json);
        Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [TestMethod]
    public async Task Pay_Twice_SecondReturns409()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var orderId = await CreateOrderAsync(user);

        var first = await user.PostAsJsonAsync($"api/orders/{orderId}/pay", CardBody(), Json);
        Assert.AreEqual(HttpStatusCode.OK, first.StatusCode);

        var second = await user.PostAsJsonAsync($"api/orders/{orderId}/pay", CardBody(), Json);
        Assert.AreEqual(HttpStatusCode.Conflict, second.StatusCode);
    }

    [TestMethod]
    public async Task Pay_WithBothCardAndPaymentMethodId_Returns422()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var orderId = await CreateOrderAsync(user);

        var body = new
        {
            card = new { name = "X", number = "4111111111111111", expiry = "2030-01", securityCode = "123" },
            paymentMethodId = 5
        };
        var resp = await user.PostAsJsonAsync($"api/orders/{orderId}/pay", body, Json);
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [TestMethod]
    public async Task Refund_SameIdempotencyKeyTwice_ReturnsSameRefund()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var admin = Client(ApiTokenHelper.GetAdminUserToken());
        var orderId = await CreateOrderAsync(user);
        await user.PostAsJsonAsync($"api/orders/{orderId}/pay", CardBody(), Json);
        await admin.PostAsync($"api/orders/{orderId}/fulfil", null);

        var body = new { amount = 0.01m, idempotencyKey = "dup-key" };
        var first = await admin.PostAsJsonAsync($"api/orders/{orderId}/refunds", body, Json);
        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        var firstId = JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("refundId").GetInt32();

        var second = await admin.PostAsJsonAsync($"api/orders/{orderId}/refunds", body, Json);
        // Replay returns the same refund (200), not a second one.
        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode);
        var secondId = JsonDocument.Parse(await second.Content.ReadAsStringAsync()).RootElement.GetProperty("refundId").GetInt32();
        Assert.AreEqual(firstId, secondId);
    }

    [TestMethod]
    public async Task SaveCard_List_Delete_ThenUnusable()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());

        var save = await user.PostAsJsonAsync("api/payment-methods", new
        {
            name = "John Doe",
            number = "4111111111111111",
            expiry = "2030-01",
            securityCode = "123",
        }, Json);
        Assert.AreEqual(HttpStatusCode.Created, save.StatusCode);
        var saveDoc = JsonDocument.Parse(await save.Content.ReadAsStringAsync());
        var pmId = saveDoc.RootElement.GetProperty("paymentMethodId").GetInt32();
        Assert.IsFalse(saveDoc.RootElement.TryGetProperty("number", out _), "response must not include the card number");

        var list = await user.GetAsync("api/payment-methods");
        var listStr = await list.Content.ReadAsStringAsync();
        StringAssert.Contains(listStr, $"\"paymentMethodId\":{pmId}");

        // Reuse the saved card to pay an order.
        var orderId = await CreateOrderAsync(user);
        var pay = await user.PostAsJsonAsync($"api/orders/{orderId}/pay", new { paymentMethodId = pmId }, Json);
        Assert.AreEqual(HttpStatusCode.OK, pay.StatusCode);

        var del = await user.DeleteAsync($"api/payment-methods/{pmId}");
        Assert.AreEqual(HttpStatusCode.NoContent, del.StatusCode);

        var listAfter = await user.GetAsync("api/payment-methods");
        var listAfterStr = await listAfter.Content.ReadAsStringAsync();
        Assert.IsFalse(listAfterStr.Contains($"\"paymentMethodId\":{pmId}"));

        // Deleted card can no longer be used to pay a new order.
        var order2 = await CreateOrderAsync(user);
        var payDeleted = await user.PostAsJsonAsync($"api/orders/{order2}/pay", new { paymentMethodId = pmId }, Json);
        Assert.AreEqual(HttpStatusCode.NotFound, payDeleted.StatusCode);
    }

    [TestMethod]
    public async Task DeleteAnotherUsersCard_Returns404()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var save = await user.PostAsJsonAsync("api/payment-methods", new
        {
            name = "John Doe", number = "4111111111111111", expiry = "2030-01", securityCode = "123",
        }, Json);
        var pmId = JsonDocument.Parse(await save.Content.ReadAsStringAsync()).RootElement.GetProperty("paymentMethodId").GetInt32();

        var other = Client(ApiTokenHelper.GetAdminUserToken());
        var del = await other.DeleteAsync($"api/payment-methods/{pmId}");
        Assert.AreEqual(HttpStatusCode.NotFound, del.StatusCode);
    }

    [TestMethod]
    public async Task MyOrders_ShowsOnlyCallersOrdersWithPaymentState()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var orderId = await CreateOrderAsync(user);
        await user.PostAsJsonAsync($"api/orders/{orderId}/pay", CardBody(), Json);

        var resp = await user.GetAsync("api/my-orders");
        Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        StringAssert.Contains(body, "PaymentAuthorized");
    }

    [TestMethod]
    public async Task Cancel_AuthorizedOrder_ReleasesAndMarksCancelled()
    {
        var user = Client(ApiTokenHelper.GetNormalUserToken());
        var admin = Client(ApiTokenHelper.GetAdminUserToken());
        var orderId = await CreateOrderAsync(user);
        await user.PostAsJsonAsync($"api/orders/{orderId}/pay", CardBody(), Json);

        var cancel = await admin.PostAsync($"api/orders/{orderId}/cancel", null);
        Assert.AreEqual(HttpStatusCode.OK, cancel.StatusCode);
        var doc = JsonDocument.Parse(await cancel.Content.ReadAsStringAsync());
        Assert.AreEqual("Cancelled", doc.RootElement.GetProperty("status").GetString());
    }
}
