using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Portfolio;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Servers;
using UpvestInvestmentApi.Signing;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Portfolio;

/// <summary>
/// Exercises <see cref="UpvestPortfolioService"/> through the SDK's HttpClient seam (a stub handler), so the
/// flows are verified without the live sandbox. Covers the request/response mapping, the error boundary, the
/// duplicate-claim guard and the sandbox-scope tolerance built into onboarding.
/// </summary>
public class UpvestPortfolioServiceTests
{
    private const string Shopper = "shopper@example.com";

    // --- A reusable stub handler (captures requests, answers with a per-test responder). ---
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, (HttpStatusCode, string)> _responder;
        public List<string> Paths { get; } = new();

        public StubHandler(Func<HttpRequestMessage, (HttpStatusCode, string)> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            var (status, json) = _responder(request);
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = request
            };
            return Task.FromResult(response);
        }
    }

    private const string TokenJson = """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}""";

    private static string EcPrivateKeyPem()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return ecdsa.ExportPkcs8PrivateKeyPem();
    }

    private static (UpvestPortfolioService service, PortfolioContext db) BuildService(
        Func<HttpRequestMessage, (HttpStatusCode, string)> responder,
        Action<PortfolioContext>? seed = null)
    {
        var db = new PortfolioContext(new DbContextOptionsBuilder<PortfolioContext>()
            .UseInMemoryDatabase($"portfolio-{Guid.NewGuid()}")
            .Options);
        seed?.Invoke(db);
        db.SaveChanges();

        var handler = new StubHandler(req =>
            req.RequestUri!.AbsolutePath.Contains("auth/token")
                ? (HttpStatusCode.OK, TokenJson)
                : responder(req));

        var options = new UpvestInvestmentApiClientOptions
        {
            Environment = ServerEnvironment.Production,
            OauthClientCredentials = new OAuth2ClientCredentials { ClientId = "cid", ClientSecret = "secret" },
            Hooks = new[] { new UpvestSigningHook(new UpvestHttpSignatureCredentials("key-1", EcPrivateKeyPem(), "cid")) }
        };
        var client = new UpvestInvestmentApiClient(new HttpClient(handler), options);

        var settings = Options.Create(new UpvestSettings
        {
            ClientId = "cid",
            ClientSecret = "secret",
            SigningKeyId = "key-1",
            SigningKeyPath = "unused",
            BaseUrl = "https://sandbox.upvest.co"
        });

        var repo = new EfPortfolioRepository(db);
        var service = new UpvestPortfolioService(client, repo, settings, NullLogger<UpvestPortfolioService>.Instance);
        return (service, db);
    }

    private static InvestorSignupForm SampleForm() => new(
        "Ann", "Lee", "ann@example.com", new DateOnly(1990, 1, 1), "Berlin", "DE", "DE",
        new InvestorAddress("1 Main St", "12345", "Berlin", "DE"),
        "491234567890", "TAX-123", "DE",
        new IdentityDocumentForm("ID_CARD", "DOC-1", "DE", new DateOnly(2032, 1, 1)));

    private static string UserTolJson(Guid id, string status) => $$"""
    {
      "id": "{{id}}",
      "created_at": "2024-01-01T00:00:00Z",
      "updated_at": "2024-01-01T00:00:00Z",
      "first_name": "Ann",
      "last_name": "Lee",
      "birth_date": "1990-01-01",
      "nationalities": ["DE"],
      "address": { "address_line1": "1 Main St", "postcode": "12345", "country": "DE", "city": "Berlin" },
      "status": "{{status}}"
    }
    """;

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task SignUp_CreatesUser_ToleratesSandboxScope_AndStaysPending()
    {
        var userId = Guid.NewGuid();
        var (service, db) = BuildService(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("/checks")) return (HttpStatusCode.Unauthorized, """{"message":"This client is not authorised to use these scopes"}""");
            if (req.Method == HttpMethod.Post && path.EndsWith("/users")) return (HttpStatusCode.OK, UserTolJson(userId, "ACTIVE"));
            if (req.Method == HttpMethod.Get && path.Contains("/users/")) return (HttpStatusCode.OK, UserTolJson(userId, "ACTIVE"));
            return (HttpStatusCode.Unauthorized, """{"message":"This client is not authorised to use these scopes"}""");
        });

        var result = await service.SignUpInvestorAsync(Shopper, SampleForm(), default);

        Assert.Equal(userId, result.InvestorId);
        Assert.Equal(InvestorSignupStatus.Pending, result.Status);

        var stored = await db.Investors.FindAsync(Shopper);
        Assert.NotNull(stored);
        Assert.Equal(userId, stored!.UpvestUserId);
    }

    [Fact]
    public async Task SignUp_Twice_IsRejectedByTheClaim()
    {
        var userId = Guid.NewGuid();
        (HttpStatusCode, string) Responder(HttpRequestMessage req)
        {
            var path = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Post && path.EndsWith("/users")) return (HttpStatusCode.OK, UserTolJson(userId, "ACTIVE"));
            if (req.Method == HttpMethod.Get && path.Contains("/users/")) return (HttpStatusCode.OK, UserTolJson(userId, "ACTIVE"));
            return (HttpStatusCode.Unauthorized, """{"message":"scopes"}""");
        }

        var (service, _) = BuildService(Responder);

        await service.SignUpInvestorAsync(Shopper, SampleForm(), default);
        await Assert.ThrowsAsync<DuplicateException>(() => service.SignUpInvestorAsync(Shopper, SampleForm(), default));
    }

    [Fact]
    public async Task SignUp_WithUnknownNationality_IsRejectedBeforeAnyCall()
    {
        var (service, _) = BuildService(_ => (HttpStatusCode.OK, "{}"));
        var badForm = SampleForm() with { Nationality = "ZZZ" };

        await Assert.ThrowsAsync<PortfolioValidationException>(() => service.SignUpInvestorAsync(Shopper, badForm, default));
    }

    [Fact]
    public async Task SignUp_MapsProviderErrorStatus()
    {
        var (service, _) = BuildService(req =>
            req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath.EndsWith("/users")
                ? (HttpStatusCode.BadRequest, """{"message":"bad"}""")
                : (HttpStatusCode.OK, "{}"));

        var ex = await Assert.ThrowsAsync<UpvestApiException>(() => service.SignUpInvestorAsync(Shopper, SampleForm(), default));
        Assert.Equal(400, ex.ProviderStatusCode);
    }

    [Fact]
    public async Task UpsertModel_WithWeightsNot100_IsRejected()
    {
        var (service, _) = BuildService(_ => (HttpStatusCode.OK, "{}"));
        var definition = new ModelPortfolioDefinition("Balanced", new[]
        {
            new ModelFundInput("DE0001", 40m),
            new ModelFundInput("DE0002", 40m) // sums to 80, not 100
        });

        await Assert.ThrowsAsync<PortfolioValidationException>(() => service.UpsertModelAsync("balanced", definition, default));
    }

    [Fact]
    public async Task PayIn_WhenNotAnInvestor_IsRejected()
    {
        var (service, _) = BuildService(_ => (HttpStatusCode.OK, "{}"));
        await Assert.ThrowsAsync<InvestorNotFoundException>(() => service.PayInAsync(Shopper, 100m, default));
    }

    [Fact]
    public async Task PayIn_WhenNoModelChosen_IsRejected()
    {
        var accountGroupId = Guid.NewGuid();
        var (service, _) = BuildService(
            req => req.RequestUri!.AbsolutePath.Contains("/account-groups/")
                ? (HttpStatusCode.OK, AccountGroupJson(accountGroupId, "ACTIVE"))
                : (HttpStatusCode.OK, "{}"),
            seed: db => db.Investors.Add(new InvestorRecord
            {
                Shopper = Shopper,
                UpvestUserId = Guid.NewGuid(),
                UpvestAccountGroupId = accountGroupId,
                UpvestAccountId = Guid.NewGuid(),
                Status = InvestorSignupStatus.Active,
                ChosenModelId = null, // no model chosen
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }));

        await Assert.ThrowsAsync<InvestorNotReadyException>(() => service.PayInAsync(Shopper, 100m, default));
    }

    [Fact]
    public async Task ChooseModel_WhenModelMissing_IsRejected()
    {
        var (service, _) = BuildService(
            _ => (HttpStatusCode.OK, "{}"),
            seed: db => db.Investors.Add(new InvestorRecord
            {
                Shopper = Shopper,
                UpvestUserId = Guid.NewGuid(),
                UpvestAccountId = Guid.NewGuid(),
                Status = InvestorSignupStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }));

        await Assert.ThrowsAsync<ModelNotFoundException>(() => service.ChooseModelAsync(Shopper, "missing", default));
    }

    private static string AccountGroupJson(Guid id, string status) => $$"""
    {
      "id": "{{id}}",
      "created_at": "2024-01-01T00:00:00Z",
      "updated_at": "2024-01-01T00:00:00Z",
      "users": [],
      "status": "{{status}}",
      "type": "PERSONAL",
      "securities_account_number": "1234567"
    }
    """;
}
