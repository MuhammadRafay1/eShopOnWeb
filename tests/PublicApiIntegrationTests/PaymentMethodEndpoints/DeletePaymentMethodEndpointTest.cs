using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.PaymentMethodEndpoints;

[TestClass]
public class DeletePaymentMethodEndpointTest
{
    [TestMethod]
    public async Task ReturnsNotFound_ForNonExistentCard()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());

        var response = await client.DeleteAsync("api/payment-methods/999999");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task ReturnsUnauthorized_WithoutToken()
    {
        var client = ProgramTest.NewClient;

        var response = await client.DeleteAsync("api/payment-methods/1");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
