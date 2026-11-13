using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Portfolio;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Core.Hooks;
using UpvestInvestmentApi.Servers;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.PortfolioTests;

/// <summary>Records every outgoing request and answers with a supplied responder (OAuth token handled for you).</summary>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string?> Bodies { get; } = new();
    public string? LastBody => Bodies.Count == 0 ? null : Bodies[^1];

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        Bodies.Add(request.Content?.ReadAsStringAsync(ct).Result);

        HttpResponseMessage response;
        var path = request.RequestUri?.AbsolutePath ?? "";
        var contentType = request.Content?.Headers.ContentType?.MediaType ?? "";
        var isTokenRequest = path.Contains("token") || path.Contains("/auth")
            || contentType == "application/x-www-form-urlencoded"
            || (Bodies[^1]?.Contains("grant_type") ?? false);
        if (isTokenRequest)
        {
            response = Json(HttpStatusCode.OK, """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}""");
        }
        else
        {
            response = _responder(request);
        }
        response.RequestMessage = request;
        return Task.FromResult(response);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}

public static class StubUpvest
{
    private static readonly string KeyPath = CreateSigningKey();

    /// <summary>A fully-constructible SDK client over a stub transport (OAuth + request signing wired as in prod).</summary>
    public static (UpvestInvestmentApiClient Client, StubHandler Handler) BuildClient(
        Func<HttpRequestMessage, HttpResponseMessage> opResponder)
    {
        var handler = new StubHandler(opResponder);
        var options = new UpvestInvestmentApiClientOptions
        {
            Environment = ServerEnvironment.Production,
            OauthClientCredentials = new OAuth2ClientCredentials { ClientId = "test-client", ClientSecret = "test-secret" },
            Hooks = new SdkHook[]
            {
                new UpvestSigningHook(UpvestHttpSignatureCredentials.FromFile("test-key", KeyPath, "test-client"))
            },
            Retry = RetryOptions.Disabled()
        };
        options.Server.Default.Production.BaseUrl = "https://sandbox.upvest.co";
        return (new UpvestInvestmentApiClient(new HttpClient(handler), options), handler);
    }

    public static PortfolioContext NewContext() =>
        new(new DbContextOptionsBuilder<PortfolioContext>()
            .UseInMemoryDatabase($"portfolio-{Guid.NewGuid()}").Options);

    public static IOptions<UpvestSettings> Settings() => Options.Create(new UpvestSettings
    {
        ClientId = "test-client",
        ClientSecret = "test-secret",
        SigningKeyId = "test-key",
        SigningKeyPath = KeyPath,
        BaseUrl = "https://sandbox.upvest.co"
    });

    private static string CreateSigningKey()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = ec.ExportECPrivateKeyPem();
        var path = Path.Combine(Path.GetTempPath(), $"upvest-test-key-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, pem);
        return path;
    }
}
