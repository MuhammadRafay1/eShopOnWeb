using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;
using Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PublicApiIntegrationTests;

namespace PublicApiIntegrationTests.PaymentMethodEndpoints;

[TestClass]
public class PaymentMethodFlowTest
{
    private static HttpClient ClientAs(string token)
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static object SampleCard => new
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
    };

    [TestMethod]
    public async Task SaveListPayDelete_Flow_Succeeds()
    {
        var shopper = ClientAs(ApiTokenHelper.GetUserToken("card-flow-buyer@test.com"));

        var saveResponse = await shopper.PostAsync("api/payment-methods", Json(new { card = SampleCard }));
        saveResponse.EnsureSuccessStatusCode();
        var saved = (await saveResponse.Content.ReadAsStringAsync()).FromJson<SavePaymentMethodResponse>();
        Assert.IsFalse(string.IsNullOrEmpty(saved!.PaymentMethodId));
        Assert.AreEqual("1111", saved.LastDigits);

        var listResponse = await shopper.GetAsync("api/payment-methods");
        listResponse.EnsureSuccessStatusCode();
        var list = (await listResponse.Content.ReadAsStringAsync()).FromJson<ListPaymentMethodsResponse>();
        Assert.IsTrue(list!.PaymentMethods.Exists(m => m.PaymentMethodId == saved.PaymentMethodId));

        // Pay a second order with the saved card instead of raw card details.
        var created = (await (await shopper.PostAsync("api/orders", Json(new
        {
            items = new[] { new { catalogItemId = 1, quantity = 1 } }
        }))).Content.ReadAsStringAsync()).FromJson<CreateOrderResponse>();

        var payResponse = await shopper.PostAsync($"api/orders/{created!.OrderId}/pay", Json(new
        {
            savedPaymentMethodId = saved.PaymentMethodId
        }));
        payResponse.EnsureSuccessStatusCode();
        var paid = (await payResponse.Content.ReadAsStringAsync()).FromJson<OrderStateDto>();
        Assert.AreEqual("Authorized", paid!.PaymentStatus);

        var deleteResponse = await shopper.DeleteAsync($"api/payment-methods/{saved.PaymentMethodId}");
        Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listAfterDelete = (await (await shopper.GetAsync("api/payment-methods")).Content.ReadAsStringAsync())
            .FromJson<ListPaymentMethodsResponse>();
        Assert.IsFalse(listAfterDelete!.PaymentMethods.Exists(m => m.PaymentMethodId == saved.PaymentMethodId));

        // A deleted card can no longer be used to pay.
        var created2 = (await (await shopper.PostAsync("api/orders", Json(new
        {
            items = new[] { new { catalogItemId = 1, quantity = 1 } }
        }))).Content.ReadAsStringAsync()).FromJson<CreateOrderResponse>();
        var payWithDeleted = await shopper.PostAsync($"api/orders/{created2!.OrderId}/pay", Json(new { savedPaymentMethodId = saved.PaymentMethodId }));
        Assert.AreEqual(HttpStatusCode.NotFound, payWithDeleted.StatusCode);
    }

    [TestMethod]
    public async Task SavedCard_NotVisibleOrDeletableByAnotherBuyer()
    {
        var owner = ClientAs(ApiTokenHelper.GetUserToken("card-owner@test.com"));
        var intruder = ClientAs(ApiTokenHelper.GetUserToken("card-intruder@test.com"));

        var saved = (await (await owner.PostAsync("api/payment-methods", Json(new { card = SampleCard })))
            .Content.ReadAsStringAsync()).FromJson<SavePaymentMethodResponse>();

        var intruderList = (await (await intruder.GetAsync("api/payment-methods")).Content.ReadAsStringAsync())
            .FromJson<ListPaymentMethodsResponse>();
        Assert.IsFalse(intruderList!.PaymentMethods.Exists(m => m.PaymentMethodId == saved!.PaymentMethodId));

        var deleteAttempt = await intruder.DeleteAsync($"api/payment-methods/{saved!.PaymentMethodId}");
        Assert.AreEqual(HttpStatusCode.NotFound, deleteAttempt.StatusCode);
    }
}
