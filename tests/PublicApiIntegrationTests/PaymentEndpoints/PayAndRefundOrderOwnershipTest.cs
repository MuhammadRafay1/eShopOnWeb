using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.OrderEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.PaymentEndpoints;

/// <summary>
/// Ownership checks happen before any PayPal call is made, so these never touch the live sandbox - a
/// mismatch is rejected purely from the order's own BuyerId.
/// </summary>
[TestClass]
public class PayAndRefundOrderOwnershipTest
{
    private static async Task<int> PlaceOrderAsync(HttpClient client)
    {
        var request = new CreateOrderRequest { Items = { new CreateOrderItemRequest { CatalogItemId = 1, Quantity = 1 } } };
        var response = await client.PostAsync("api/orders",
            new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadAsStringAsync()).FromJson<CreateOrderResponse>();
        return created!.OrderId;
    }

    [TestMethod]
    public async Task Pay_ReturnsNotFound_WhenOrderBelongsToAnotherBuyer()
    {
        var owner = ProgramTest.NewClient;
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());
        var orderId = await PlaceOrderAsync(owner);

        var otherBuyer = ProgramTest.NewClient;
        otherBuyer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetOtherNormalUserToken());

        var payBody = new StringContent("""{ "card": { "number": "4111111111111111", "expiry": "2030-01", "securityCode": "123" } }""", Encoding.UTF8, "application/json");
        var response = await otherBuyer.PostAsync($"api/orders/{orderId}/pay", payBody);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Pay_ReturnsBadRequest_WhenBothCardAndPaymentMethodSupplied()
    {
        var owner = ProgramTest.NewClient;
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());
        var orderId = await PlaceOrderAsync(owner);

        var payBody = new StringContent(
            """{ "card": { "number": "4111111111111111", "expiry": "2030-01" }, "paymentMethodId": 1 }""",
            Encoding.UTF8, "application/json");
        var response = await owner.PostAsync($"api/orders/{orderId}/pay", payBody);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Refund_ReturnsNotFound_WhenOrderBelongsToAnotherBuyer()
    {
        var owner = ProgramTest.NewClient;
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());
        var orderId = await PlaceOrderAsync(owner);

        var otherBuyer = ProgramTest.NewClient;
        otherBuyer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetOtherNormalUserToken());

        var refundBody = new StringContent("""{ "idempotencyKey": "test-key-1" }""", Encoding.UTF8, "application/json");
        var response = await otherBuyer.PostAsync($"api/orders/{orderId}/refunds", refundBody);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Refund_ReturnsBadRequest_WhenIdempotencyKeyMissing()
    {
        var owner = ProgramTest.NewClient;
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());
        var orderId = await PlaceOrderAsync(owner);

        var refundBody = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await owner.PostAsync($"api/orders/{orderId}/refunds", refundBody);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
