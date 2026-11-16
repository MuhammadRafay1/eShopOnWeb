using System;
using System.Net;

namespace Microsoft.eShopWeb.PublicApi.Portfolio;

/// <summary>
/// The single failure type the Upvest gateway raises. It carries the HTTP status (when the provider
/// answered) and a caller-safe provider message, so endpoints can map one coherent ladder of
/// outcomes without handling every SDK exception type. It never carries a request body or secret.
/// </summary>
public sealed class UpvestGatewayException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    /// <summary>True when the outcome of a write is unknown (the connection failed after the
    /// provider may already have acted).</summary>
    public bool OutcomeUnknown { get; }

    public UpvestGatewayException(string message, HttpStatusCode? statusCode, Exception? inner = null, bool outcomeUnknown = false)
        : base(message, inner)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
    }
}
