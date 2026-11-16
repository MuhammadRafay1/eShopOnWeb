using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.Portfolio;

[TestClass]
public class PortfolioEndpointsTests
{
    private const string ValidSignup = """
    {
      "firstName":"Ada","lastName":"Lovelace","email":"ada@example.com",
      "birthDate":"1985-12-10","birthCity":"Berlin","birthCountry":"DE","nationality":"DE",
      "address":{"line1":"Teststrasse 1","postcode":"10115","city":"Berlin","country":"DE"},
      "phoneNumber":"+49 30 1234567","taxId":"12345678901","taxCountry":"DE",
      "identityDocument":{"type":"ID_CARD","number":"DE1234567","issuingCountry":"DE","expiryDate":"2030-01-01"}
    }
    """;

    private const string Model =
        """{"name":"Balanced Growth","funds":[{"instrumentId":"IE00B4L5Y983","weight":60},{"instrumentId":"IE00B3RBWM25","weight":40}]}""";

    private static HttpClient Client(PortfolioApiFactory f, string token)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> Body(HttpResponseMessage r)
        => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    [TestMethod]
    public async Task SignUp_Then_GetInvestor_ReturnsActive()
    {
        using var f = new PortfolioApiFactory();
        var user = Client(f, ApiTokenHelper.GetNormalUserToken());

        var post = await user.PostAsync("api/portfolio/investor", Json(ValidSignup));
        Assert.AreEqual(HttpStatusCode.OK, post.StatusCode);
        var body = await Body(post);
        Assert.AreEqual("user-1", body.GetProperty("investorId").GetString());
        Assert.AreEqual("active", body.GetProperty("status").GetString());

        var get = await user.GetAsync("api/portfolio/investor");
        Assert.AreEqual(HttpStatusCode.OK, get.StatusCode);
        Assert.AreEqual("active", (await Body(get)).GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task SignUp_Pending_WhenUpvestNotReady()
    {
        using var f = new PortfolioApiFactory();
        f.Upvest.UserStatus = "INACTIVE";
        f.Upvest.AccountStatus = "PENDING_APPROVAL";
        var user = Client(f, ApiTokenHelper.GetNormalUserToken());

        var post = await user.PostAsync("api/portfolio/investor", Json(ValidSignup));
        Assert.AreEqual("pending", (await Body(post)).GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task SignUp_MissingFields_Returns400()
    {
        using var f = new PortfolioApiFactory();
        var user = Client(f, ApiTokenHelper.GetNormalUserToken());
        var post = await user.PostAsync("api/portfolio/investor", Json("""{"firstName":"X"}"""));
        Assert.AreEqual(HttpStatusCode.BadRequest, post.StatusCode);
    }

    [TestMethod]
    public async Task GetInvestor_BeforeSignup_Returns404()
    {
        using var f = new PortfolioApiFactory();
        var user = Client(f, ApiTokenHelper.GetNormalUserToken());
        var get = await user.GetAsync("api/portfolio/investor");
        Assert.AreEqual(HttpStatusCode.NotFound, get.StatusCode);
    }

    [TestMethod]
    public async Task PutModel_RequiresAdmin()
    {
        using var f = new PortfolioApiFactory();
        var normal = Client(f, ApiTokenHelper.GetNormalUserToken());
        var forbidden = await normal.PutAsync("api/portfolio/models/balanced", Json(Model));
        Assert.AreEqual(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var admin = Client(f, ApiTokenHelper.GetAdminUserToken());
        var ok = await admin.PutAsync("api/portfolio/models/balanced", Json(Model));
        Assert.AreEqual(HttpStatusCode.OK, ok.StatusCode);
        var body = await Body(ok);
        Assert.AreEqual("balanced", body.GetProperty("modelId").GetString());
        Assert.AreEqual(2, body.GetProperty("funds").GetArrayLength());
    }

    [TestMethod]
    public async Task PutModel_WeightsNotSumming100_Returns400()
    {
        using var f = new PortfolioApiFactory();
        var admin = Client(f, ApiTokenHelper.GetAdminUserToken());
        var bad = """{"name":"Bad","funds":[{"instrumentId":"IE00B4L5Y983","weight":70},{"instrumentId":"IE00B3RBWM25","weight":40}]}""";
        var r = await admin.PutAsync("api/portfolio/models/bad", Json(bad));
        Assert.AreEqual(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [TestMethod]
    public async Task ListModels_ReturnsCreatedModels()
    {
        using var f = new PortfolioApiFactory();
        await Client(f, ApiTokenHelper.GetAdminUserToken()).PutAsync("api/portfolio/models/balanced", Json(Model));

        var list = await Client(f, ApiTokenHelper.GetNormalUserToken()).GetAsync("api/portfolio/models");
        Assert.AreEqual(HttpStatusCode.OK, list.StatusCode);
        var arr = await Body(list);
        Assert.AreEqual(1, arr.GetArrayLength());
        Assert.AreEqual("balanced", arr[0].GetProperty("modelId").GetString());
    }

    [TestMethod]
    public async Task ChooseModel_UnknownModel_Returns404()
    {
        using var f = new PortfolioApiFactory();
        var user = Client(f, ApiTokenHelper.GetNormalUserToken());
        await user.PostAsync("api/portfolio/investor", Json(ValidSignup));
        var r = await user.PutAsync("api/portfolio/investor/model", Json("""{"modelId":"nope"}"""));
        Assert.AreEqual(HttpStatusCode.NotFound, r.StatusCode);
    }

    [TestMethod]
    public async Task ChooseModel_BeforeSignup_Returns409()
    {
        using var f = new PortfolioApiFactory();
        await Client(f, ApiTokenHelper.GetAdminUserToken()).PutAsync("api/portfolio/models/balanced", Json(Model));
        var user = Client(f, ApiTokenHelper.GetNormalUserToken());
        var r = await user.PutAsync("api/portfolio/investor/model", Json("""{"modelId":"balanced"}"""));
        Assert.AreEqual(HttpStatusCode.Conflict, r.StatusCode);
    }

    [TestMethod]
    public async Task FullHappyPath_PayInInvested_And_PortfolioReflectsIt()
    {
        using var f = new PortfolioApiFactory();
        var admin = Client(f, ApiTokenHelper.GetAdminUserToken());
        var user = Client(f, ApiTokenHelper.GetNormalUserToken());

        await user.PostAsync("api/portfolio/investor", Json(ValidSignup));
        await admin.PutAsync("api/portfolio/models/balanced", Json(Model));
        var choose = await user.PutAsync("api/portfolio/investor/model", Json("""{"modelId":"balanced"}"""));
        Assert.AreEqual(HttpStatusCode.OK, choose.StatusCode);

        var pay = await user.PostAsync("api/portfolio/payments", Json("""{"amount":100.00}"""));
        Assert.AreEqual(HttpStatusCode.OK, pay.StatusCode);
        var payBody = await Body(pay);
        Assert.AreEqual("invested", payBody.GetProperty("status").GetString());
        Assert.AreEqual(100.00m, payBody.GetProperty("amount").GetDecimal());

        var list = await user.GetAsync("api/portfolio/payments");
        var listBody = await Body(list);
        Assert.AreEqual(1, listBody.GetArrayLength());
        Assert.AreEqual("invested", listBody[0].GetProperty("status").GetString());

        var portfolio = await user.GetAsync("api/portfolio");
        var pf = await Body(portfolio);
        Assert.AreEqual("balanced", pf.GetProperty("modelId").GetString());
        Assert.AreEqual(150.00m, pf.GetProperty("totalValue").GetDecimal());
        Assert.AreEqual(100.00m, pf.GetProperty("paidIn").GetDecimal());
        Assert.IsTrue(pf.GetProperty("holdings").GetArrayLength() >= 1);
    }

    [TestMethod]
    public async Task PayIn_WithoutActiveInvestor_Returns409()
    {
        using var f = new PortfolioApiFactory();
        f.Upvest.UserStatus = "INACTIVE";
        var user = Client(f, ApiTokenHelper.GetNormalUserToken());
        await user.PostAsync("api/portfolio/investor", Json(ValidSignup));
        var pay = await user.PostAsync("api/portfolio/payments", Json("""{"amount":50.00}"""));
        Assert.AreEqual(HttpStatusCode.Conflict, pay.StatusCode);
    }

    [TestMethod]
    public async Task Realignment_AdminTriggers_And_StatusIsReadable()
    {
        using var f = new PortfolioApiFactory();
        var admin = Client(f, ApiTokenHelper.GetAdminUserToken());
        await admin.PutAsync("api/portfolio/models/balanced", Json(Model));

        var trigger = await admin.PostAsync("api/portfolio/models/balanced/realignments", null);
        Assert.AreEqual(HttpStatusCode.OK, trigger.StatusCode);
        var tb = await Body(trigger);
        int id = tb.GetProperty("realignmentId").GetInt32();

        var get = await Client(f, ApiTokenHelper.GetNormalUserToken()).GetAsync($"api/portfolio/realignments/{id}");
        Assert.AreEqual(HttpStatusCode.OK, get.StatusCode);
        Assert.AreEqual("completed", (await Body(get)).GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task Realignment_RequiresAdmin()
    {
        using var f = new PortfolioApiFactory();
        var normal = Client(f, ApiTokenHelper.GetNormalUserToken());
        var r = await normal.PostAsync("api/portfolio/models/balanced/realignments", null);
        Assert.AreEqual(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [TestMethod]
    public async Task Endpoints_RequireAuthentication()
    {
        using var f = new PortfolioApiFactory();
        var anon = f.CreateClient();
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await anon.GetAsync("api/portfolio/investor")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await anon.GetAsync("api/portfolio")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await anon.GetAsync("api/portfolio/models")).StatusCode);
    }
}
