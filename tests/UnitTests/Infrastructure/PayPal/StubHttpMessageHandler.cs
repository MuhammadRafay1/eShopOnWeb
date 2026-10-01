using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.PayPal;

/// <summary>
/// Test seam for the PayPal SDK: an HttpMessageHandler that records every outgoing request (and its body,
/// buffered before the SDK disposes the content) and replies from a caller-supplied responder.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _responder;

    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string?> Bodies { get; } = new();

    public StubHttpMessageHandler(Func<HttpRequestMessage, string?, HttpResponseMessage> responder) => _responder = responder;

    /// <summary>Always reply with the given status and JSON body.</summary>
    public static StubHttpMessageHandler Returning(HttpStatusCode status, string json) =>
        new((_, _) => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

    /// <summary>Always throw a transport failure (no usable response).</summary>
    public static StubHttpMessageHandler Throwing() =>
        new((_, _) => throw new HttpRequestException("connection reset"));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content?.ReadAsStringAsync(cancellationToken).Result);
        var response = _responder(request, Bodies[^1]);
        response.RequestMessage = request;
        return Task.FromResult(response);
    }

    public string? HeaderOf(int index, string name) =>
        Requests[index].Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
