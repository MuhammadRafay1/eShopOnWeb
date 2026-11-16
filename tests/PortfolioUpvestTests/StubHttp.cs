using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.eShopWeb.PublicApi.Portfolio;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Core.Authentication.OAuth2.ClientCredentials;
using UpvestInvestmentApi.Core.Configuration;
using UpvestInvestmentApi.Core.Hooks;
using UpvestInvestmentApi.Signing;

namespace Microsoft.eShopWeb.PortfolioUpvestTests;

/// <summary>
/// A stub <see cref="HttpMessageHandler"/> that answers the OAuth token request automatically and
/// drives every other request through a queue of responders (each may return a response or throw),
/// capturing the outgoing method/path/body for assertions.
/// </summary>
public sealed class StubHttp : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string?> Bodies { get; } = new();
    public HttpRequestMessage? LastApiRequest { get; private set; }
    public string? LastApiBody { get; private set; }

    public StubHttp Enqueue(HttpStatusCode status, string json) =>
        Enqueue(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });

    public StubHttp Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responders.Enqueue(responder);
        return this;
    }

    public StubHttp Throw(Exception ex) => Enqueue(_ => throw ex);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        var body = request.Content is null ? null : request.Content.ReadAsStringAsync(ct).Result;
        Bodies.Add(body);

        // Answer the token request transparently so the first API call is the one under test.
        if (request.RequestUri!.AbsolutePath.EndsWith("/auth/token"))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"access_token\":\"test-token\",\"token_type\":\"Bearer\",\"expires_in\":3600}",
                    System.Text.Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
        }

        LastApiRequest = request;
        LastApiBody = body;
        var responder = _responders.Dequeue();
        var response = responder(request);
        response.RequestMessage = request;
        return Task.FromResult(response);
    }
}

public static class TestGateway
{
    /// <summary>Builds a real gateway over a stubbed HttpClient, with an ephemeral EC signing key so
    /// the SDK's mandatory request signing succeeds without any network or real credential.</summary>
    public static UpvestInvestmentGateway Create(StubHttp stub)
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = ec.ExportECPrivateKeyPem();
        var signing = new UpvestHttpSignatureCredentials("test-key", pem, "test-client");

        var options = new UpvestInvestmentApiClientOptions
        {
            OauthClientCredentials = new OAuth2ClientCredentials { ClientId = "test-client", ClientSecret = "test-secret" },
            Hooks = new SdkHook[] { new UpvestSigningHook(signing) },
            Retry = RetryOptions.Default() with { Timeout = TimeSpan.FromSeconds(5), MaxRetries = 0 },
            Logging = new LoggingOptions { LoggerFactory = NullLoggerFactory.Instance, LogRequestBody = false },
        };
        options.Server.Default.Production.BaseUrl = "https://sandbox.upvest.co";

        var client = new UpvestInvestmentApiClient(new HttpClient(stub), options);
        var settings = Options.Create(new UpvestSettings { BaseUrl = "https://sandbox.upvest.co" });
        return new UpvestInvestmentGateway(client, settings, NullLogger<UpvestInvestmentGateway>.Instance);
    }
}
