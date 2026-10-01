using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.PaymentMethodEndpoints;

/// <summary>
/// Only the paths that never reach PayPal (list and delete-when-absent) are exercised here; saving a
/// card is verified manually against the live sandbox (see the verification guide).
/// </summary>
[TestClass]
public class PaymentMethodEndpointTest
{
    [TestMethod]
    public async Task List_ReturnsNotAuthorizedWithoutToken()
    {
        var client = ProgramTest.NewClient;
        var response = await client.GetAsync("api/payment-methods");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task List_ReturnsEmptyArray_ForBuyerWithNoSavedCards()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetOtherNormalUserToken());

        var response = await client.GetAsync("api/payment-methods");
        response.EnsureSuccessStatusCode();

        var body = (await response.Content.ReadAsStringAsync()).FromJson<ListPaymentMethodsResponse>();
        Assert.IsNotNull(body);
        Assert.AreEqual(0, body!.PaymentMethods.Length);
    }

    [TestMethod]
    public async Task Delete_ReturnsNotFound_WhenNoSuchSavedCard()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());

        var response = await client.DeleteAsync("api/payment-methods/999999");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
