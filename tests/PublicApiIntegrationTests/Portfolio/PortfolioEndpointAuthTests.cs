using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.Portfolio;

/// <summary>
/// Endpoint-level authorization and validation, driven through the real PublicApi host. These paths are deterministic
/// (they resolve before any Upvest call), so they do not depend on the sandbox.
/// </summary>
[TestClass]
public class PortfolioEndpointAuthTests
{
    private static HttpClient Client(string? token)
    {
        var client = ProgramTest.NewClient;
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent Json(string s) => new(s, Encoding.UTF8, "application/json");

    [TestMethod]
    public async Task GetInvestor_WithoutToken_Returns401()
    {
        var response = await Client(null).GetAsync("api/portfolio/investor");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task PutModel_AsNormalUser_Returns403()
    {
        var client = Client(ApiTokenHelper.GetNormalUserToken());
        var response = await client.PutAsync("api/portfolio/models/any",
            Json("{\"name\":\"X\",\"funds\":[{\"instrumentId\":\"US0378331005\",\"weight\":100}]}"));
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PutModel_AsAdminWithBadWeights_Returns400()
    {
        var client = Client(ApiTokenHelper.GetAdminUserToken());
        var response = await client.PutAsync("api/portfolio/models/badweights",
            Json("{\"name\":\"X\",\"funds\":[{\"instrumentId\":\"US0378331005\",\"weight\":50},{\"instrumentId\":\"US5949181045\",\"weight\":40}]}"));
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task RealignmentTrigger_AsNormalUser_Returns403()
    {
        var client = Client(ApiTokenHelper.GetNormalUserToken());
        var response = await client.PostAsync("api/portfolio/models/any/realignments", Json("{}"));
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task GetModels_AsNormalUser_Returns200()
    {
        var client = Client(ApiTokenHelper.GetNormalUserToken());
        var response = await client.GetAsync("api/portfolio/models");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
