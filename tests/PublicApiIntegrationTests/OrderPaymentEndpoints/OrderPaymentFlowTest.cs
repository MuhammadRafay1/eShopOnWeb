using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.OrderPaymentEndpoints;

[TestClass]
public class OrderPaymentFlowTest
{
    private static HttpClient ClientAs(string token)
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    [TestMethod]
    public async Task FullFlow_CreatePayFulfilPartialRefund_Succeeds()
    {
        var shopper = ClientAs(ApiTokenHelper.GetNormalUserToken());
        var admin = ClientAs(ApiTokenHelper.GetAdminUserToken());

        var createResponse = await shopper.PostAsync("api/orders", Json(new
        {
            items = new[] { new { catalogItemId = 1, quantity = 1 } },
            shipTo = new { street = "1 Main St", city = "Seattle", state = "WA", country = "US", zipCode = "98101" }
        }));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadAsStringAsync()).FromJson<CreateOrderResponse>();
        Assert.IsTrue(created!.OrderId > 0);

        var payResponse = await shopper.PostAsync($"api/orders/{created.OrderId}/pay", Json(new
        {
            card = new
            {
                number = "4111111111111111",
                expiry = "2030-12",
                securityCode = "123",
                cardholderName = "Test Buyer",
                street = "1 Main St",
                city = "Seattle",
                state = "WA",
                country = "US",
                postalCode = "98101"
            }
        }));
        payResponse.EnsureSuccessStatusCode();
        var paid = (await payResponse.Content.ReadAsStringAsync()).FromJson<OrderStateDto>();
        Assert.AreEqual("Authorized", paid!.PaymentStatus);
        Assert.IsNotNull(paid.AuthorizationId);

        var fulfilResponse = await admin.PostAsync($"api/orders/{created.OrderId}/fulfil", null);
        fulfilResponse.EnsureSuccessStatusCode();
        var fulfilled = (await fulfilResponse.Content.ReadAsStringAsync()).FromJson<OrderStateDto>();
        Assert.AreEqual("Captured", fulfilled!.PaymentStatus);
        Assert.AreEqual(paid.Total, fulfilled.CapturedGross);
        Assert.IsNotNull(fulfilled.NetAmount);

        var refundResponse = await admin.PostAsync($"api/orders/{created.OrderId}/refunds", Json(new
        {
            amount = 1.00m,
            idempotencyKey = $"test-refund-{created.OrderId}"
        }));
        refundResponse.EnsureSuccessStatusCode();
        var refund = (await refundResponse.Content.ReadAsStringAsync()).FromJson<RefundOrderResponse>();
        Assert.IsFalse(string.IsNullOrEmpty(refund!.RefundId));
        Assert.AreEqual(1.00m, refund.Amount);

        // Repeating the exact same idempotency key must not refund a second time.
        var repeatRefund = await admin.PostAsync($"api/orders/{created.OrderId}/refunds", Json(new
        {
            amount = 1.00m,
            idempotencyKey = $"test-refund-{created.OrderId}"
        }));
        repeatRefund.EnsureSuccessStatusCode();
        var repeat = (await repeatRefund.Content.ReadAsStringAsync()).FromJson<RefundOrderResponse>();
        Assert.AreEqual(refund.RefundId, repeat!.RefundId);
        Assert.AreEqual(refund.Amount, repeat.Amount);

        var myOrders = await shopper.GetAsync("api/my-orders");
        myOrders.EnsureSuccessStatusCode();
        var mine = (await myOrders.Content.ReadAsStringAsync()).FromJson<MyOrdersResponse>();
        Assert.IsTrue(mine!.Orders.Exists(o => o.OrderId == created.OrderId && o.RefundedTotal == 1.00m));
    }

    [TestMethod]
    public async Task RefundReplay_ViaServiceDirectly_NeverDoubleRefunds()
    {
        // Drives OrderPaymentService directly (rather than through HTTP) across two separate DI scopes,
        // so each call gets its own fresh repositories - the scenario a real repeated client request produces.
        var shopper = ClientAs(ApiTokenHelper.GetNormalUserToken());
        var admin = ClientAs(ApiTokenHelper.GetAdminUserToken());

        var created = (await (await shopper.PostAsync("api/orders", Json(new
        {
            items = new[] { new { catalogItemId = 1, quantity = 1 } }
        }))).Content.ReadAsStringAsync()).FromJson<CreateOrderResponse>();

        await shopper.PostAsync($"api/orders/{created!.OrderId}/pay", Json(new
        {
            card = new { number = "4111111111111111", expiry = "2030-12", securityCode = "123", cardholderName = "T", street = "1 Main St", city = "Seattle", country = "US", postalCode = "98101" }
        }));
        await admin.PostAsync($"api/orders/{created.OrderId}/fulfil", null);

        var key = $"direct-service-refund-{created.OrderId}";

        using (var scope1 = ProgramTest.Services.CreateScope())
        {
            var svc1 = scope1.ServiceProvider.GetRequiredService<IOrderPaymentService>();
            var outcome1 = await svc1.RefundAsync(created.OrderId, 2.00m, key, default);
            Assert.AreEqual(2.00m, outcome1.Amount);
        }

        using (var scope2 = ProgramTest.Services.CreateScope())
        {
            var svc2 = scope2.ServiceProvider.GetRequiredService<IOrderPaymentService>();
            var outcome2 = await svc2.RefundAsync(created.OrderId, 2.00m, key, default);
            // The money-safety guarantee under test: repeating the same idempotency key across two
            // independent scopes/requests must not move money a second time, regardless of which
            // status string the replay reports for the already-settled claim.
            Assert.AreEqual(2.00m, outcome2.Amount);
        }

        var mine = (await (await shopper.GetAsync("api/my-orders")).Content.ReadAsStringAsync()).FromJson<MyOrdersResponse>();
        var order = mine!.Orders.Single(o => o.OrderId == created.OrderId);
        Assert.AreEqual(2.00m, order.RefundedTotal); // not 4.00m - the replay did not refund a second time
    }

    [TestMethod]
    public async Task RefundExceedingCapturedAmount_IsRejected()
    {
        var shopper = ClientAs(ApiTokenHelper.GetNormalUserToken());
        var admin = ClientAs(ApiTokenHelper.GetAdminUserToken());

        var created = (await (await shopper.PostAsync("api/orders", Json(new
        {
            items = new[] { new { catalogItemId = 2, quantity = 1 } }
        }))).Content.ReadAsStringAsync()).FromJson<CreateOrderResponse>();

        await shopper.PostAsync($"api/orders/{created!.OrderId}/pay", Json(new
        {
            card = new { number = "4111111111111111", expiry = "2030-12", securityCode = "123", cardholderName = "T", street = "1 Main St", city = "Seattle", country = "US", postalCode = "98101" }
        }));
        await admin.PostAsync($"api/orders/{created.OrderId}/fulfil", null);

        var overRefund = await admin.PostAsync($"api/orders/{created.OrderId}/refunds", Json(new
        {
            amount = 999m,
            idempotencyKey = $"test-overrefund-{created.OrderId}"
        }));

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, overRefund.StatusCode);
    }

    [TestMethod]
    public async Task Pay_OnAnotherBuyersOrder_ReturnsNotFound()
    {
        var owner = ClientAs(ApiTokenHelper.GetNormalUserToken());
        var intruder = ClientAs(ApiTokenHelper.GetUserToken("intruder@test.com"));

        var created = (await (await owner.PostAsync("api/orders", Json(new
        {
            items = new[] { new { catalogItemId = 3, quantity = 1 } }
        }))).Content.ReadAsStringAsync()).FromJson<CreateOrderResponse>();

        var response = await intruder.PostAsync($"api/orders/{created!.OrderId}/pay", Json(new
        {
            card = new { number = "4111111111111111", expiry = "2030-12", securityCode = "123", cardholderName = "T", street = "x", city = "x", country = "US", postalCode = "1" }
        }));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Fulfil_AsShopper_IsForbidden()
    {
        var shopper = ClientAs(ApiTokenHelper.GetNormalUserToken());
        var response = await shopper.PostAsync("api/orders/1/fulfil", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Reconciliation_AsShopper_IsForbidden()
    {
        var shopper = ClientAs(ApiTokenHelper.GetNormalUserToken());
        var response = await shopper.GetAsync("api/reconciliation?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z");
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PayWithBothCardAndSavedMethod_IsRejected()
    {
        var shopper = ClientAs(ApiTokenHelper.GetNormalUserToken());
        var created = (await (await shopper.PostAsync("api/orders", Json(new
        {
            items = new[] { new { catalogItemId = 4, quantity = 1 } }
        }))).Content.ReadAsStringAsync()).FromJson<CreateOrderResponse>();

        var response = await shopper.PostAsync($"api/orders/{created!.OrderId}/pay", Json(new
        {
            card = new { number = "4111111111111111", expiry = "2030-12", securityCode = "123", cardholderName = "T", street = "x", city = "x", country = "US", postalCode = "1" },
            savedPaymentMethodId = "some-id"
        }));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
