using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.PortfolioEndpoints;

/// <summary>
/// Deterministic endpoint tests for the managed-portfolio capability: authorization gating and
/// shop-side validation. These paths short-circuit before any Upvest call, so they do not depend on
/// the sandbox.
/// </summary>
[TestClass]
public class PortfolioEndpointsTest
{
    private static HttpClient Client(string? token)
    {
        var client = ProgramTest.NewClient;
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    [TestMethod]
    public async Task UpsertModel_WithNormalUser_ReturnsForbidden()
    {
        var response = await Client(ApiTokenHelper.GetNormalUserToken())
            .PutAsync("api/portfolio/models/model-normal", Json(new { name = "M", funds = new[] { new { instrumentId = "X", weight = 100 } } }));

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task UpsertModel_WithWeightsNotTotalling100_ReturnsBadRequest()
    {
        var response = await Client(ApiTokenHelper.GetAdminUserToken())
            .PutAsync("api/portfolio/models/model-badweights", Json(new
            {
                name = "Bad",
                funds = new[]
                {
                    new { instrumentId = "IE00B4L5Y983", weight = 60 },
                    new { instrumentId = "IE00B3RBWM25", weight = 30 }
                }
            }));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task UpsertModel_WithNoFunds_ReturnsBadRequest()
    {
        var response = await Client(ApiTokenHelper.GetAdminUserToken())
            .PutAsync("api/portfolio/models/model-nofunds", Json(new { name = "Empty", funds = new object[0] }));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task ListModels_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Client(null).GetAsync("api/portfolio/models");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task ListModels_WithToken_ReturnsOk()
    {
        var response = await Client(ApiTokenHelper.GetNormalUserToken()).GetAsync("api/portfolio/models");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task TriggerRealignment_WithNormalUser_ReturnsForbidden()
    {
        var response = await Client(ApiTokenHelper.GetNormalUserToken())
            .PostAsync("api/portfolio/models/balanced/realignments", content: null);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task GetRealignment_Unknown_ReturnsNotFound()
    {
        var response = await Client(ApiTokenHelper.GetNormalUserToken())
            .GetAsync("api/portfolio/realignments/11111111-1111-1111-1111-111111111111");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task PayIn_WhenNotAnInvestor_ReturnsNotFound()
    {
        // demouser is never signed up in these tests, so they are not an investor.
        var response = await Client(ApiTokenHelper.GetNormalUserToken())
            .PostAsync("api/portfolio/payments", Json(new { amount = 100.00 }));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetInvestor_WhenNotAnInvestor_ReturnsNotFound()
    {
        var response = await Client(ApiTokenHelper.GetNormalUserToken()).GetAsync("api/portfolio/investor");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}
