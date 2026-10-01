using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.OrderEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.OrderEndpoints;

[TestClass]
public class CreateOrderEndpointTest
{
    private static HttpClient AuthenticatedClient(string token)
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [TestMethod]
    public async Task ReturnsNotAuthorizedWithoutToken()
    {
        var client = ProgramTest.NewClient;
        var response = await client.PostAsync("api/orders", JsonBody(new CreateOrderRequest { Items = { new CreateOrderItemRequest { CatalogItemId = 1, Quantity = 1 } } }));

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task PlacesOrderFromCatalogItems_PriceComesFromCatalog()
    {
        var client = AuthenticatedClient(ApiTokenHelper.GetNormalUserToken());
        var request = new CreateOrderRequest
        {
            Items = { new CreateOrderItemRequest { CatalogItemId = 1, Quantity = 2 } }
        };

        var response = await client.PostAsync("api/orders", JsonBody(request));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        var model = body.FromJson<CreateOrderResponse>();

        Assert.IsNotNull(model);
        Assert.IsTrue(model!.OrderId > 0);
        Assert.IsTrue(model.Total > 0, "Total should be computed from the catalog item's price, not supplied by the caller.");
        Assert.AreEqual("AwaitingPayment", model.PaymentStatus);
    }

    private static StringContent JsonBody<T>(T value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
