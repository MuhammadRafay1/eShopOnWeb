using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb.PublicApi.OrderEndpoints;
using Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;
using Microsoft.eShopWeb.PublicApi.PaymentModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.Payments;

[TestClass]
public class PaymentFlowTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static PaymentApiFactory _factory = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => _factory = new PaymentApiFactory();

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    private static HttpClient Client(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent Body(object o) =>
        new(JsonSerializer.Serialize(o), Encoding.UTF8, "application/json");

    private static object SampleOrderRequest() => new
    {
        catalogItems = new[] { new { catalogItemId = 1, quantity = 2 } },
        shippingAddress = new
        {
            street = "1 Test St",
            city = "Redmond",
            state = "WA",
            country = "US",
            zipCode = "98052"
        }
    };

    private static object SampleCard() => new
    {
        number = "4111111111111111",
        expiry = "2030-01",
        securityCode = "123",
        name = "Test Shopper",
        billingAddress = new
        {
            addressLine1 = "1 Test St",
            city = "Redmond",
            state = "WA",
            postalCode = "98052",
            countryCode = "US"
        }
    };

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    private async Task<int> CreateOrderAsync(HttpClient client)
    {
        var response = await client.PostAsync("api/orders", Body(SampleOrderRequest()));
        response.EnsureSuccessStatusCode();
        var created = await Read<CreateOrderResponse>(response);
        Assert.IsTrue(created.OrderId > 0, "orderId should be a positive top-level field");
        return created.OrderId;
    }

    [TestMethod]
    public async Task FullPayFulfilRefundHappyPath()
    {
        var shopper = Client(ApiTokenHelper.GetNormalUserToken());
        var admin = Client(ApiTokenHelper.GetAdminUserToken());

        var orderId = await CreateOrderAsync(shopper);

        // Pay (authorize)
        var pay = await shopper.PostAsync($"api/orders/{orderId}/pay", Body(new { card = SampleCard() }));
        pay.EnsureSuccessStatusCode();
        var afterPay = await Read<OrderView>(pay);
        Assert.AreEqual("PaymentAuthorized", afterPay.Status);
        Assert.AreEqual("Created", afterPay.Payment!.AuthorizationStatus);

        // Fulfil (capture) - admin only
        var fulfil = await admin.PostAsync($"api/orders/{orderId}/fulfil", content: null);
        fulfil.EnsureSuccessStatusCode();
        var afterFulfil = await Read<OrderView>(fulfil);
        Assert.AreEqual("Fulfilled", afterFulfil.Status);
        Assert.IsNotNull(afterFulfil.Payment!.CapturedGrossAmount);
        Assert.IsNotNull(afterFulfil.Payment!.PayPalFee);
        Assert.IsNotNull(afterFulfil.Payment!.NetAmount);
        Assert.AreEqual(afterFulfil.Total, afterFulfil.Payment!.CapturedGrossAmount);

        // Partial refund
        var refund = await shopper.PostAsync($"api/orders/{orderId}/refunds",
            Body(new { amount = 1.00m, idempotencyKey = "refund-key-1" }));
        refund.EnsureSuccessStatusCode();
        var refundResult = await Read<RefundOrderResponse>(refund);
        Assert.IsTrue(refundResult.RefundId > 0, "refundId should be a positive top-level field");
        Assert.AreEqual("PartiallyRefunded", refundResult.OrderStatus);
    }

    [TestMethod]
    public async Task RefundIsIdempotentUnderSameKey()
    {
        var shopper = Client(ApiTokenHelper.GetNormalUserToken());
        var admin = Client(ApiTokenHelper.GetAdminUserToken());
        var orderId = await CreateOrderAsync(shopper);
        (await shopper.PostAsync($"api/orders/{orderId}/pay", Body(new { card = SampleCard() }))).EnsureSuccessStatusCode();
        (await admin.PostAsync($"api/orders/{orderId}/fulfil", null)).EnsureSuccessStatusCode();

        var first = await shopper.PostAsync($"api/orders/{orderId}/refunds",
            Body(new { amount = 2.00m, idempotencyKey = "dupe-key" }));
        first.EnsureSuccessStatusCode();
        var second = await shopper.PostAsync($"api/orders/{orderId}/refunds",
            Body(new { amount = 2.00m, idempotencyKey = "dupe-key" }));
        second.EnsureSuccessStatusCode();

        var r1 = await Read<RefundOrderResponse>(first);
        var r2 = await Read<RefundOrderResponse>(second);
        Assert.AreEqual(r1.RefundId, r2.RefundId);
        Assert.AreEqual(r1.PayPalRefundId, r2.PayPalRefundId);
    }

    [TestMethod]
    public async Task RefundBeyondCapturedIsRejected()
    {
        var shopper = Client(ApiTokenHelper.GetNormalUserToken());
        var admin = Client(ApiTokenHelper.GetAdminUserToken());
        var orderId = await CreateOrderAsync(shopper);
        (await shopper.PostAsync($"api/orders/{orderId}/pay", Body(new { card = SampleCard() }))).EnsureSuccessStatusCode();
        (await admin.PostAsync($"api/orders/{orderId}/fulfil", null)).EnsureSuccessStatusCode();

        var response = await shopper.PostAsync($"api/orders/{orderId}/refunds",
            Body(new { amount = 999999.00m, idempotencyKey = "too-much" }));
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
    }

    [TestMethod]
    public async Task SavedCardCanBeCreatedListedUsedAndDeleted()
    {
        var shopper = Client(ApiTokenHelper.GetNormalUserToken("saver@microsoft.com"));

        // Save a card
        var save = await shopper.PostAsync("api/payment-methods", Body(new { card = SampleCard(), alias = "my visa" }));
        save.EnsureSuccessStatusCode();
        var saved = await Read<CreatePaymentMethodResponse>(save);
        Assert.IsTrue(saved.PaymentMethodId > 0);
        Assert.AreEqual("1111", saved.Last4);
        Assert.IsFalse(saved.ToString()!.Contains("4111111111111111"));

        // List
        var list = await shopper.GetAsync("api/payment-methods");
        list.EnsureSuccessStatusCode();
        var cards = await Read<List<SavedCardView>>(list);
        Assert.AreEqual(1, cards.Count);
        Assert.AreEqual(saved.PaymentMethodId, cards[0].PaymentMethodId);

        // Pay a new order with the saved card
        var orderId = await CreateOrderAsync(shopper);
        var pay = await shopper.PostAsync($"api/orders/{orderId}/pay",
            Body(new { paymentMethodId = saved.PaymentMethodId }));
        pay.EnsureSuccessStatusCode();
        var afterPay = await Read<OrderView>(pay);
        Assert.AreEqual("PaymentAuthorized", afterPay.Status);

        // Delete
        var del = await shopper.DeleteAsync($"api/payment-methods/{saved.PaymentMethodId}");
        Assert.AreEqual(HttpStatusCode.NoContent, del.StatusCode);

        // Gone from list
        var listAfter = await Read<List<SavedCardView>>(await shopper.GetAsync("api/payment-methods"));
        Assert.AreEqual(0, listAfter.Count);

        // No longer usable to pay
        var order2 = await CreateOrderAsync(shopper);
        var payDeleted = await shopper.PostAsync($"api/orders/{order2}/pay",
            Body(new { paymentMethodId = saved.PaymentMethodId }));
        Assert.AreEqual(HttpStatusCode.NotFound, payDeleted.StatusCode);
    }

    [TestMethod]
    public async Task OperatorActionsRejectNormalUser()
    {
        var shopper = Client(ApiTokenHelper.GetNormalUserToken());
        var orderId = await CreateOrderAsync(shopper);

        Assert.AreEqual(HttpStatusCode.Forbidden,
            (await shopper.PostAsync($"api/orders/{orderId}/fulfil", null)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden,
            (await shopper.PostAsync($"api/orders/{orderId}/cancel", null)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden,
            (await shopper.GetAsync("api/reconciliation?from=2020-01-01T00:00:00Z&to=2020-02-01T00:00:00Z")).StatusCode);
    }

    [TestMethod]
    public async Task ShopperCannotActOnAnotherShoppersOrder()
    {
        var owner = Client(ApiTokenHelper.GetNormalUserToken("owner@microsoft.com"));
        var other = Client(ApiTokenHelper.GetNormalUserToken("intruder@microsoft.com"));

        var orderId = await CreateOrderAsync(owner);

        // Intruder cannot pay the owner's order.
        var pay = await other.PostAsync($"api/orders/{orderId}/pay", Body(new { card = SampleCard() }));
        Assert.AreEqual(HttpStatusCode.NotFound, pay.StatusCode);

        // Intruder's my-orders does not include the owner's order.
        var mine = await Read<List<OrderView>>(await other.GetAsync("api/my-orders"));
        Assert.IsFalse(mine.Exists(o => o.OrderId == orderId));
    }

    [TestMethod]
    public async Task CancelReleasesAuthorizationBeforeFulfilment()
    {
        var shopper = Client(ApiTokenHelper.GetNormalUserToken());
        var admin = Client(ApiTokenHelper.GetAdminUserToken());
        var orderId = await CreateOrderAsync(shopper);
        (await shopper.PostAsync($"api/orders/{orderId}/pay", Body(new { card = SampleCard() }))).EnsureSuccessStatusCode();

        var cancel = await admin.PostAsync($"api/orders/{orderId}/cancel", null);
        cancel.EnsureSuccessStatusCode();
        var afterCancel = await Read<OrderView>(cancel);
        Assert.AreEqual("Cancelled", afterCancel.Status);
        Assert.AreEqual("Voided", afterCancel.Payment!.AuthorizationStatus);
    }

    [TestMethod]
    public async Task PayRequiresExactlyOnePaymentSource()
    {
        var shopper = Client(ApiTokenHelper.GetNormalUserToken());
        var orderId = await CreateOrderAsync(shopper);

        var neither = await shopper.PostAsync($"api/orders/{orderId}/pay", Body(new { }));
        Assert.AreEqual(HttpStatusCode.BadRequest, neither.StatusCode);
    }
}
