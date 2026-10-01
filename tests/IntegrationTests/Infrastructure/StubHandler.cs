#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.IntegrationTests.Infrastructure;

/// <summary>
/// Minimal fake HttpMessageHandler for testing PayPalPaymentGateway without a real network call - the
/// seam documented by the paypal-sdk dotnet-testing skill. Supports a queue of responders so a test can
/// simulate "fails once, then succeeds".
/// </summary>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders;

    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string?> Bodies { get; } = new();
    public HttpRequestMessage? LastRequest => Requests.Count == 0 ? null : Requests[^1];
    public string? LastBody => Bodies.Count == 0 ? null : Bodies[^1];

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responders = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(new[] { responder });
    }

    public StubHandler(IEnumerable<Func<HttpRequestMessage, HttpResponseMessage>> responders)
    {
        _responders = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responders);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? null : request.Content.ReadAsStringAsync(cancellationToken).Result);

        var responder = _responders.Count > 1 ? _responders.Dequeue() : _responders.Peek();
        var response = responder(request);
        response.RequestMessage = request;
        return Task.FromResult(response);
    }
}
