using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.PaymentEndpoints;

[TestClass]
public class FulfilAndCancelOrderEndpointTest
{
    [TestMethod]
    public async Task Fulfil_ReturnsForbidden_ForNormalUser()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());

        var response = await client.PostAsync("api/orders/999999/fulfil", null);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Fulfil_ReturnsNotFound_ForAdmin_WhenOrderDoesNotExist()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetAdminUserToken());

        var response = await client.PostAsync("api/orders/999999/fulfil", null);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_ReturnsForbidden_ForNormalUser()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());

        var response = await client.PostAsync("api/orders/999999/cancel", null);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task Cancel_ReturnsNotFound_ForAdmin_WhenOrderDoesNotExist()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetAdminUserToken());

        var response = await client.PostAsync("api/orders/999999/cancel", null);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
