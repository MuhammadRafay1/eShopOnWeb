using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Constants;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.Portfolio;

[TestClass]
public class PortfolioEndpointsTests
{
    private static string Token(string userName, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, userName) };
        foreach (var role in roles) claims.Add(new Claim(ClaimTypes.Role, role));

        var key = Encoding.ASCII.GetBytes(AuthorizationConstants.JWT_SECRET_KEY);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims.ToArray()),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static HttpClient Client(PortfolioTestApplication app, string userName, params string[] roles)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(userName, roles));
        return client;
    }

    private static object SampleModelBody() => new
    {
        name = "Balanced",
        funds = new[]
        {
            new { instrumentId = "DE0000000001", weight = 60m },
            new { instrumentId = "DE0000000002", weight = 40m },
        },
    };

    private static object SampleInvestorBody() => new
    {
        firstName = "Ada",
        lastName = "Lovelace",
        email = "ada@example.com",
        birthDate = "1990-01-01",
        birthCity = "London",
        birthCountry = "GB",
        nationality = "GB",
        address = new { line1 = "1 Analytical Way", postcode = "EC1A1AA", city = "London", country = "GB" },
        phoneNumber = "441234567890",
        taxId = "TAX123",
        taxCountry = "GB",
        identityDocument = new { type = "PASSPORT", number = "P1234567", issuingCountry = "GB", expiryDate = "2032-01-01" },
    };

    [TestMethod]
    public async Task PutModel_AsNormalUser_IsForbidden()
    {
        using var app = new PortfolioTestApplication();
        var client = Client(app, "shopper1@example.com");

        var response = await client.PutAsJsonAsync("api/portfolio/models/balanced", SampleModelBody());

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PutModel_AsAdmin_CreatesModel()
    {
        using var app = new PortfolioTestApplication();
        var client = Client(app, "admin@microsoft.com", "Administrators");

        var response = await client.PutAsJsonAsync("api/portfolio/models/balanced", SampleModelBody());

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("balanced", json.GetProperty("modelId").GetString());
        Assert.AreEqual("Balanced", json.GetProperty("name").GetString());
        Assert.AreEqual(2, json.GetProperty("funds").GetArrayLength());
    }

    [TestMethod]
    public async Task PutModel_WeightsNotSum100_IsBadRequest()
    {
        using var app = new PortfolioTestApplication();
        var client = Client(app, "admin@microsoft.com", "Administrators");

        var body = new
        {
            name = "Bad",
            funds = new[] { new { instrumentId = "DE0000000001", weight = 60m }, new { instrumentId = "DE0000000002", weight = 30m } },
        };
        var response = await client.PutAsJsonAsync("api/portfolio/models/bad", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task SignUpInvestor_ThenGet_ReturnsInvestorIdAndStatus()
    {
        using var app = new PortfolioTestApplication();
        var client = Client(app, "shopper2@example.com");

        var post = await client.PostAsJsonAsync("api/portfolio/investor", SampleInvestorBody());
        Assert.AreEqual(HttpStatusCode.OK, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<JsonElement>();
        var investorId = created.GetProperty("investorId").GetGuid();
        Assert.AreNotEqual(Guid.Empty, investorId);
        Assert.AreEqual("active", created.GetProperty("status").GetString());

        var get = await client.GetAsync("api/portfolio/investor");
        Assert.AreEqual(HttpStatusCode.OK, get.StatusCode);
        var fetched = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(investorId, fetched.GetProperty("investorId").GetGuid());
    }

    [TestMethod]
    public async Task GetInvestor_BeforeSignUp_IsNotFound()
    {
        using var app = new PortfolioTestApplication();
        var client = Client(app, "never-signed-up@example.com");

        var response = await client.GetAsync("api/portfolio/investor");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task FullFlow_SignUp_ChooseModel_PayIn_Portfolio()
    {
        using var app = new PortfolioTestApplication();
        var shopper = Client(app, "shopper3@example.com");
        var admin = Client(app, "admin@microsoft.com", "Administrators");

        // Operator defines a model.
        var model = await admin.PutAsJsonAsync("api/portfolio/models/growth", SampleModelBody());
        Assert.AreEqual(HttpStatusCode.OK, model.StatusCode);

        // Shopper signs up (fake Upvest reports Active).
        var signup = await shopper.PostAsJsonAsync("api/portfolio/investor", SampleInvestorBody());
        Assert.AreEqual(HttpStatusCode.OK, signup.StatusCode);

        // Shopper chooses the model.
        var choose = await shopper.PutAsJsonAsync("api/portfolio/investor/model", new { modelId = "growth" });
        Assert.AreEqual(HttpStatusCode.OK, choose.StatusCode);

        // Shopper pays in.
        var pay = await shopper.PostAsJsonAsync("api/portfolio/payments", new { amount = 100.00m });
        Assert.AreEqual(HttpStatusCode.OK, pay.StatusCode);
        var payment = await pay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("invested", payment.GetProperty("status").GetString());
        Assert.AreNotEqual(Guid.Empty, payment.GetProperty("paymentId").GetGuid());

        // Payments list shows it.
        var payments = await (await shopper.GetAsync("api/portfolio/payments")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(1, payments.GetArrayLength());
        Assert.AreEqual(100.00m, payments[0].GetProperty("amount").GetDecimal());

        // Portfolio view.
        var portfolio = await (await shopper.GetAsync("api/portfolio")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(150.00m, portfolio.GetProperty("totalValue").GetDecimal());
        Assert.AreEqual(100.00m, portfolio.GetProperty("paidIn").GetDecimal());
        Assert.AreEqual(1, portfolio.GetProperty("holdings").GetArrayLength());
    }

    [TestMethod]
    public async Task PayIn_WhenNotActive_IsRefused()
    {
        using var app = new PortfolioTestApplication();
        app.Upvest.InvestorStatus = Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate.InvestorStatus.Pending;
        var shopper = Client(app, "shopper4@example.com");

        await shopper.PostAsJsonAsync("api/portfolio/investor", SampleInvestorBody());
        var pay = await shopper.PostAsJsonAsync("api/portfolio/payments", new { amount = 50.00m });

        Assert.AreEqual(HttpStatusCode.Conflict, pay.StatusCode);
    }

    [TestMethod]
    public async Task Realignment_AsAdmin_ThenGet_ReportsStatus()
    {
        using var app = new PortfolioTestApplication();
        var admin = Client(app, "admin@microsoft.com", "Administrators");

        await admin.PutAsJsonAsync("api/portfolio/models/income", SampleModelBody());

        var trigger = await admin.PostAsync("api/portfolio/models/income/realignments", null);
        Assert.AreEqual(HttpStatusCode.OK, trigger.StatusCode);
        var triggered = await trigger.Content.ReadFromJsonAsync<JsonElement>();
        var realignmentId = triggered.GetProperty("realignmentId").GetGuid();
        // The trigger response is pending — the outcome at Upvest is not yet known.
        Assert.AreEqual("pending", triggered.GetProperty("status").GetString());

        // The GET re-reads Upvest and resolves the outcome.
        var get = await admin.GetAsync($"api/portfolio/realignments/{realignmentId}");
        Assert.AreEqual(HttpStatusCode.OK, get.StatusCode);
        var fetched = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("completed", fetched.GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task Realignment_AsNormalUser_IsForbidden()
    {
        using var app = new PortfolioTestApplication();
        var shopper = Client(app, "shopper5@example.com");

        var response = await shopper.PostAsync("api/portfolio/models/income/realignments", null);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
