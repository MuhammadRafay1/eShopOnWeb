using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.OrderEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.OrderEndpoints;

[TestClass]
public class OrderAuthorizationAndOwnershipTest
{
    private static async Task<int> CreateOrderAsync(string token)
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new CreateOrderRequest { Items = new List<CreateOrderItemDto> { new() { CatalogItemId = 1, Quantity = 1 } } };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("api/orders", content);
        response.EnsureSuccessStatusCode();
        var model = (await response.Content.ReadAsStringAsync()).FromJson<CreateOrderResponse>();
        return model!.OrderId;
    }

    [TestMethod]
    public async Task NormalUser_GetsForbidden_OnFulfil()
    {
        var orderId = await CreateOrderAsync(ApiTokenHelper.GetNormalUserToken());

        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());
        var response = await client.PostAsync($"api/orders/{orderId}/fulfil", null);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task NormalUser_GetsForbidden_OnCancel()
    {
        var orderId = await CreateOrderAsync(ApiTokenHelper.GetNormalUserToken());

        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());
        var response = await client.PostAsync($"api/orders/{orderId}/cancel", null);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task NormalUser_GetsForbidden_OnReconciliation()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());
        var response = await client.GetAsync("api/reconciliation?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z");

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task OtherShopper_CannotPayAnotherBuyersOrder()
    {
        var orderId = await CreateOrderAsync(ApiTokenHelper.GetNormalUserToken());

        var otherToken = ApiTokenHelper.GetTokenForUser("other-shopper@example.com");
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var content = new StringContent(JsonSerializer.Serialize(new
        {
            cardNumber = "4111111111111111",
            expiryYearMonth = "2030-01",
            cardholderName = "Someone Else"
        }), Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"api/orders/{orderId}/pay", content);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task OtherShopper_CannotRefundAnotherBuyersOrder()
    {
        var orderId = await CreateOrderAsync(ApiTokenHelper.GetNormalUserToken());

        var otherToken = ApiTokenHelper.GetTokenForUser("other-shopper-2@example.com");
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var content = new StringContent(JsonSerializer.Serialize(new { idempotencyKey = "irrelevant" }), Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"api/orders/{orderId}/refunds", content);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task OtherShopper_DoesNotSeeAnotherBuyersOrder_InMyOrders()
    {
        var orderId = await CreateOrderAsync(ApiTokenHelper.GetNormalUserToken());

        var otherToken = ApiTokenHelper.GetTokenForUser("other-shopper-3@example.com");
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);

        var response = await client.GetAsync("api/my-orders");
        response.EnsureSuccessStatusCode();
        var model = (await response.Content.ReadAsStringAsync()).FromJson<MyOrdersResponse>();

        Assert.IsNotNull(model);
        Assert.IsFalse(model!.Orders.Exists(o => o.OrderId == orderId));
    }
}
