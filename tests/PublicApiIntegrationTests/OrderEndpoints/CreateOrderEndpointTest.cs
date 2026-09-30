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
public class CreateOrderEndpointTest
{
    [TestMethod]
    public async Task ReturnsOrderIdAndAwaitingPaymentStatus()
    {
        var token = ApiTokenHelper.GetNormalUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreateOrderRequest { Items = new List<CreateOrderItemDto> { new() { CatalogItemId = 1, Quantity = 1 } } };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("api/orders", content);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var model = body.FromJson<CreateOrderResponse>();

        Assert.IsNotNull(model);
        Assert.IsTrue(model!.OrderId > 0);
        Assert.AreEqual("AwaitingPayment", model.Status);
    }

    [TestMethod]
    public async Task ReturnsUnauthorized_WithoutToken()
    {
        var client = ProgramTest.NewClient;
        var request = new CreateOrderRequest { Items = new List<CreateOrderItemDto> { new() { CatalogItemId = 1, Quantity = 1 } } };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("api/orders", content);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
