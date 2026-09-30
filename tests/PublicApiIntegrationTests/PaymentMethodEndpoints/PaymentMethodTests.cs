using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.PaymentMethodEndpoints;

[TestClass]
public class PaymentMethodTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly PaymentApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient ClientFor(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object SaveCard => new
    {
        cardholderName = "Test Buyer",
        cardNumber = "4111111111111111",
        expiry = "2027-12",
        securityCode = "123"
    };

    private async Task<int> SaveCardAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("api/payment-methods", SaveCard, Json);
        Assert.AreEqual(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.AreEqual("1111", body.GetProperty("lastDigits").GetString());
        Assert.IsFalse(body.TryGetProperty("payPalVaultId", out _), "vault id must never be returned");
        return body.GetProperty("paymentMethodId").GetInt32();
    }

    [TestMethod]
    public async Task Save_List_Delete_RoundTrips()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var id = await SaveCardAsync(shopper);

        var listed = await shopper.GetFromJsonAsync<JsonElement>("api/payment-methods", Json);
        Assert.AreEqual(1, listed.GetProperty("paymentMethods").GetArrayLength());

        var del = await shopper.DeleteAsync($"api/payment-methods/{id}");
        Assert.AreEqual(HttpStatusCode.NoContent, del.StatusCode);

        var afterDelete = await shopper.GetFromJsonAsync<JsonElement>("api/payment-methods", Json);
        Assert.AreEqual(0, afterDelete.GetProperty("paymentMethods").GetArrayLength());
        Assert.AreEqual(1, _factory.Gateway.DeletedVaultIds.Count); // deleted at PayPal too
    }

    [TestMethod]
    public async Task SavedCard_PaysASecondOrder()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var paymentMethodId = await SaveCardAsync(shopper);

        var create = await shopper.PostAsJsonAsync("api/orders",
            new { items = new[] { new { catalogItemId = 2, quantity = 1 } } }, Json);
        var orderId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("orderId").GetInt32();

        var pay = await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", new { paymentMethodId }, Json);
        Assert.AreEqual(HttpStatusCode.OK, pay.StatusCode);
        var body = await pay.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.AreEqual("PaymentAuthorized", body.GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task Shopper_CannotDeleteAnothersCard()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var other = ClientFor(ApiTokenHelper.GetAdminUserToken());
        var id = await SaveCardAsync(shopper);

        var del = await other.DeleteAsync($"api/payment-methods/{id}");
        Assert.AreEqual(HttpStatusCode.NotFound, del.StatusCode);
    }

    [TestMethod]
    public async Task PayReferencingAnothersCard_IsRejected()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var other = ClientFor(ApiTokenHelper.GetAdminUserToken());
        var otherCardId = await SaveCardAsync(other);

        var create = await shopper.PostAsJsonAsync("api/orders",
            new { items = new[] { new { catalogItemId = 1, quantity = 1 } } }, Json);
        var orderId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("orderId").GetInt32();

        var pay = await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay", new { paymentMethodId = otherCardId }, Json);
        Assert.AreEqual(HttpStatusCode.NotFound, pay.StatusCode);
    }
}
