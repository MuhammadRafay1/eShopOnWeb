using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.ReconciliationEndpoints;

[TestClass]
public class ReconciliationTests : IDisposable
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

    private static string Range()
    {
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        return $"api/reconciliation?from={from}&to={to}";
    }

    [TestMethod]
    public async Task Reconciliation_RequiresAdmin()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var resp = await shopper.GetAsync(Range());
        Assert.AreEqual(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [TestMethod]
    public async Task LocalCaptureShowsAsEShopOnly_WhenPayPalHasNotReportedIt()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var admin = ClientFor(ApiTokenHelper.GetAdminUserToken());

        // Produce a real local capture in range.
        var create = await shopper.PostAsJsonAsync("api/orders",
            new { items = new[] { new { catalogItemId = 1, quantity = 1 } } }, Json);
        var orderId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("orderId").GetInt32();
        await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay",
            new { card = new { name = "T", number = "4111111111111111", expiry = "2027-12", securityCode = "123" } }, Json);
        await admin.PostAsync($"api/orders/{orderId}/fulfil", null);

        // PayPal reports nothing (empty) — the capture must surface as EShopOnly, an expected result.
        var report = await admin.GetFromJsonAsync<JsonElement>(Range(), Json);
        Assert.IsTrue(report.GetProperty("summary").GetProperty("eShopOnlyCount").GetInt32() >= 1);
    }

    [TestMethod]
    public async Task MatchesPayPalTransactionAgainstLocalCapture()
    {
        var shopper = ClientFor(ApiTokenHelper.GetNormalUserToken());
        var admin = ClientFor(ApiTokenHelper.GetAdminUserToken());

        var create = await shopper.PostAsJsonAsync("api/orders",
            new { items = new[] { new { catalogItemId = 1, quantity = 1 } } }, Json);
        var orderId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("orderId").GetInt32();
        await shopper.PostAsJsonAsync($"api/orders/{orderId}/pay",
            new { card = new { name = "T", number = "4111111111111111", expiry = "2027-12", securityCode = "123" } }, Json);
        await admin.PostAsync($"api/orders/{orderId}/fulfil", null);

        // The fake capture id is CAP-AUTH-1; make PayPal "know" it so the row matches.
        _factory.Gateway.Transactions.Add(new ReconciliationTransaction("CAP-AUTH-1", 1m, "USD", "S", DateTimeOffset.UtcNow));

        var report = await admin.GetFromJsonAsync<JsonElement>(Range(), Json);
        Assert.IsTrue(report.GetProperty("summary").GetProperty("matchedCount").GetInt32() >= 1);
    }

    [TestMethod]
    public async Task InvalidRange_IsBadRequest()
    {
        var admin = ClientFor(ApiTokenHelper.GetAdminUserToken());
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var resp = await admin.GetAsync($"api/reconciliation?from={from}&to={to}");
        Assert.AreEqual(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
