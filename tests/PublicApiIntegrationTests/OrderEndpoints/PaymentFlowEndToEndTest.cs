using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.OrderEndpoints;

/// <summary>
/// Drives the full payment surface end-to-end against the real PayPal sandbox using the sandbox
/// Visa test card. Skips (Inconclusive) when PayPal credentials are not present in the environment,
/// so `dotnet test` remains runnable elsewhere.
/// </summary>
[TestClass]
public class PaymentFlowEndToEndTest
{
    private static bool CredentialsPresent =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PAYPAL_CLIENT_ID")) &&
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PAYPAL_CLIENT_SECRET"));

    private const string CardJson =
        "{\"name\":\"John Doe\",\"number\":\"4111111111111111\",\"expiry\":\"2028-04\",\"securityCode\":\"123\"," +
        "\"billingAddress\":{\"addressLine1\":\"1 Main\",\"adminArea2\":\"Redmond\",\"adminArea1\":\"WA\",\"postalCode\":\"98052\",\"countryCode\":\"US\"}}";

    private static HttpClient Client(string token)
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [TestMethod]
    public async Task PayCaptureRefund_And_SavedCardReuse_WorkAgainstSandbox()
    {
        if (!CredentialsPresent)
        {
            Assert.Inconclusive("PayPal sandbox credentials not present; skipping live integration test.");
        }

        var shopper = Client(ApiTokenHelper.GetNormalUserToken());
        var admin = Client(ApiTokenHelper.GetAdminUserToken());

        // 1. Place an order.
        var placeResp = await shopper.PostAsync("api/orders", Json(
            "{\"shipToAddress\":{\"street\":\"1 Main\",\"city\":\"Redmond\",\"state\":\"WA\",\"country\":\"US\",\"zipCode\":\"98052\"}," +
            "\"items\":[{\"catalogItemId\":1,\"quantity\":1},{\"catalogItemId\":2,\"quantity\":2}]}"));
        Assert.AreEqual(HttpStatusCode.Created, placeResp.StatusCode);
        var order = await ReadJson(placeResp);
        var orderId = order.GetProperty("orderId").GetInt32();

        // 2. Pay (authorize) with the sandbox card — places a hold equal to the order total.
        var payResp = await shopper.PostAsync($"api/orders/{orderId}/pay", Json("{\"card\":" + CardJson + "}"));
        Assert.AreEqual(HttpStatusCode.OK, payResp.StatusCode, await payResp.Content.ReadAsStringAsync());
        var pay = await ReadJson(payResp);
        Assert.AreEqual("Authorized", pay.GetProperty("status").GetString());
        var authId = pay.GetProperty("payment").GetProperty("authorizationId").GetString();
        Assert.IsFalse(string.IsNullOrEmpty(authId));

        // 3. Fulfil (capture) as an operator — money is taken; fee & net are reported.
        var fulfilResp = await admin.PostAsync($"api/orders/{orderId}/fulfil", null);
        Assert.AreEqual(HttpStatusCode.OK, fulfilResp.StatusCode, await fulfilResp.Content.ReadAsStringAsync());
        var fulfil = await ReadJson(fulfilResp);
        var payment = fulfil.GetProperty("payment");
        Assert.IsFalse(string.IsNullOrEmpty(payment.GetProperty("captureId").GetString()));
        Assert.IsTrue(payment.GetProperty("capturedAmount").GetDecimal() > 0);
        Assert.IsTrue(payment.TryGetProperty("netAmount", out _));

        // 4. Partial refund + idempotency replay under the same key.
        var refund1 = await shopper.PostAsync($"api/orders/{orderId}/refunds", Json("{\"idempotencyKey\":\"it-k1\",\"amount\":5.00}"));
        Assert.AreEqual(HttpStatusCode.Created, refund1.StatusCode, await refund1.Content.ReadAsStringAsync());
        var refundId1 = (await ReadJson(refund1)).GetProperty("refundId").GetString();

        var refund1Replay = await shopper.PostAsync($"api/orders/{orderId}/refunds", Json("{\"idempotencyKey\":\"it-k1\",\"amount\":5.00}"));
        Assert.AreEqual(HttpStatusCode.Created, refund1Replay.StatusCode);
        var refundId1Replay = (await ReadJson(refund1Replay)).GetProperty("refundId").GetString();
        Assert.AreEqual(refundId1, refundId1Replay, "Replay under the same idempotency key must not create a second refund.");

        // 5. Save a card, then reuse it to pay a second order.
        var saveResp = await shopper.PostAsync("api/payment-methods", Json("{\"card\":" + CardJson + "}"));
        Assert.AreEqual(HttpStatusCode.Created, saveResp.StatusCode, await saveResp.Content.ReadAsStringAsync());
        var saved = await ReadJson(saveResp);
        var paymentMethodId = saved.GetProperty("paymentMethodId").GetInt32();
        Assert.AreEqual("1111", saved.GetProperty("last4").GetString());

        var order2Resp = await shopper.PostAsync("api/orders", Json(
            "{\"shipToAddress\":{\"street\":\"2 Oak\",\"city\":\"Bellevue\",\"state\":\"WA\",\"country\":\"US\",\"zipCode\":\"98004\"}," +
            "\"items\":[{\"catalogItemId\":3,\"quantity\":1}]}"));
        var orderId2 = (await ReadJson(order2Resp)).GetProperty("orderId").GetInt32();

        var paySavedResp = await shopper.PostAsync($"api/orders/{orderId2}/pay", Json($"{{\"paymentMethodId\":{paymentMethodId}}}"));
        Assert.AreEqual(HttpStatusCode.OK, paySavedResp.StatusCode, await paySavedResp.Content.ReadAsStringAsync());
        Assert.AreEqual("Authorized", (await ReadJson(paySavedResp)).GetProperty("status").GetString());

        // 6. Delete the saved card; it must then be unusable to pay.
        var deleteResp = await shopper.DeleteAsync($"api/payment-methods/{paymentMethodId}");
        Assert.AreEqual(HttpStatusCode.OK, deleteResp.StatusCode);

        var order3Resp = await shopper.PostAsync("api/orders", Json(
            "{\"shipToAddress\":{\"street\":\"3 Pine\",\"city\":\"Kirkland\",\"state\":\"WA\",\"country\":\"US\",\"zipCode\":\"98033\"}," +
            "\"items\":[{\"catalogItemId\":4,\"quantity\":1}]}"));
        var orderId3 = (await ReadJson(order3Resp)).GetProperty("orderId").GetInt32();
        var payDeletedResp = await shopper.PostAsync($"api/orders/{orderId3}/pay", Json($"{{\"paymentMethodId\":{paymentMethodId}}}"));
        Assert.AreEqual(HttpStatusCode.NotFound, payDeletedResp.StatusCode);
    }

    [TestMethod]
    public async Task Reconciliation_IsAdminOnly()
    {
        var shopper = Client(ApiTokenHelper.GetNormalUserToken());
        var from = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var resp = await shopper.GetAsync($"api/reconciliation?from={from}&to={to}");
        Assert.AreEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [TestMethod]
    public async Task Pay_OrderNotOwned_Returns404()
    {
        if (!CredentialsPresent)
        {
            Assert.Inconclusive("PayPal sandbox credentials not present; skipping live integration test.");
        }

        // Shopper places an order; admin (a different identity) must not be able to pay it.
        var shopper = Client(ApiTokenHelper.GetNormalUserToken());
        var admin = Client(ApiTokenHelper.GetAdminUserToken());

        var placeResp = await shopper.PostAsync("api/orders", Json(
            "{\"shipToAddress\":{\"street\":\"1 Main\",\"city\":\"Redmond\",\"state\":\"WA\",\"country\":\"US\",\"zipCode\":\"98052\"}," +
            "\"items\":[{\"catalogItemId\":1,\"quantity\":1}]}"));
        var orderId = (await ReadJson(placeResp)).GetProperty("orderId").GetInt32();

        var payResp = await admin.PostAsync($"api/orders/{orderId}/pay", Json("{\"card\":" + CardJson + "}"));
        Assert.AreEqual(HttpStatusCode.NotFound, payResp.StatusCode);
    }
}
