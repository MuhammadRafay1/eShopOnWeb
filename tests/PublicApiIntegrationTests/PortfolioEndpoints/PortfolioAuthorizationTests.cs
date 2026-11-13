using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.PortfolioEndpoints;

/// <summary>
/// Verifies the operator-vs-shopper authorization split on the managed-portfolio endpoints.
/// These requests are rejected at the authorization layer before any Upvest call is made, so they
/// exercise the routing and role restrictions without touching the sandbox.
/// </summary>
[TestClass]
public class PortfolioAuthorizationTests
{
    private static HttpClient Client => ProgramTest.NewClient;

    private static StringContent EmptyJson() => new("{}", Encoding.UTF8, "application/json");

    [TestMethod]
    public async Task InvestorSignUp_WithoutToken_IsUnauthorized()
    {
        var response = await Client.PostAsync("api/portfolio/investor", EmptyJson());
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetInvestor_WithoutToken_IsUnauthorized()
    {
        var response = await Client.GetAsync("api/portfolio/investor");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task UpsertModel_AsNormalUser_IsForbidden()
    {
        var client = Client;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());

        var body = new StringContent(
            "{\"name\":\"Balanced\",\"funds\":[{\"instrumentId\":\"IE00B4L5Y983\",\"weight\":100}]}",
            Encoding.UTF8, "application/json");
        var response = await client.PutAsync("api/portfolio/models/balanced", body);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task TriggerRealignment_AsNormalUser_IsForbidden()
    {
        var client = Client;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiTokenHelper.GetNormalUserToken());

        var response = await client.PostAsync("api/portfolio/models/balanced/realignments", EmptyJson());

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task UpsertModel_WithoutToken_IsUnauthorized()
    {
        var body = new StringContent(
            "{\"name\":\"Balanced\",\"funds\":[{\"instrumentId\":\"IE00B4L5Y983\",\"weight\":100}]}",
            Encoding.UTF8, "application/json");
        var response = await Client.PutAsync("api/portfolio/models/balanced", body);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPortfolio_WithoutToken_IsUnauthorized()
    {
        var response = await Client.GetAsync("api/portfolio");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
