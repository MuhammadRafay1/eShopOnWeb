using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.OrderEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.OrderEndpoints;

[TestClass]
public class MyOrdersEndpointTest
{
    [TestMethod]
    public async Task ReturnsNotAuthorizedWithoutToken()
    {
        var client = ProgramTest.NewClient;
        var response = await client.GetAsync("api/my-orders");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ReturnsOnlyTheCallersOwnOrders_AwaitingPaymentUntilPaid()
    {
        var token = ApiTokenHelper.GetNormalUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createRequest = new CreateOrderRequest { Items = { new CreateOrderItemRequest { CatalogItemId = 1, Quantity = 1 } } };
        var createResponse = await client.PostAsync("api/orders",
            new StringContent(JsonSerializer.Serialize(createRequest), Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadAsStringAsync()).FromJson<CreateOrderResponse>();

        var listResponse = await client.GetAsync("api/my-orders");
        listResponse.EnsureSuccessStatusCode();
        var list = (await listResponse.Content.ReadAsStringAsync()).FromJson<MyOrdersResponse>();

        Assert.IsNotNull(list);
        var placed = list!.Orders.SingleOrDefault(o => o.OrderId == created!.OrderId);
        Assert.IsNotNull(placed, "The order the caller just placed must appear in their own order list.");
        Assert.IsNull(placed!.Payment, "A freshly placed order has no payment record until it is paid.");
    }
}
