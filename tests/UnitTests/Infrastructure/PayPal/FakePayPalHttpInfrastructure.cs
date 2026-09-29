using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.Infrastructure.Payments.PayPal;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.PayPal;

/// <summary>
/// A fake HttpMessageHandler that matches on (method, path substring) and returns canned JSON.
/// Records the requests it saw so tests can assert on what was sent.
/// </summary>
public class FakePayPalHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> match, HttpStatusCode status, string body)> _rules = new();
    public List<HttpRequestMessage> Requests { get; } = new();

    /// <summary>Request bodies captured at send time (before the client disposes the content).</summary>
    public List<string> RequestBodies { get; } = new();

    public FakePayPalHandler When(HttpMethod method, string pathContains, HttpStatusCode status, string body)
    {
        _rules.Add((req => req.Method == method && (req.RequestUri?.ToString().Contains(pathContains) ?? false),
            status, body));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        RequestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

        foreach (var (match, status, body) in _rules)
        {
            if (match(request))
            {
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
                };
            }
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"name\":\"NOT_FOUND\"}")
        };
    }
}

/// <summary>An IHttpClientFactory that hands out one HttpClient wrapping the fake handler.</summary>
public class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false)
    {
        BaseAddress = new Uri("https://api-m.sandbox.paypal.example/")
    };
}

public class StubTokenProvider : IPayPalAccessTokenProvider
{
    public Task<string> GetAccessTokenAsync(CancellationToken ct) => Task.FromResult("stub-token");
}
