using System;
using System.Net;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Raised when Upvest returns a non-success response. Carries only the status code and Upvest's
/// correlation id — never the request/response bodies, which may contain personal data.
/// </summary>
public sealed class UpvestApiException : Exception
{
    public UpvestApiException(HttpStatusCode statusCode, string operation, string? requestId)
        : base($"Upvest call '{operation}' failed with status {(int)statusCode}" +
               (requestId is null ? "." : $" (upvest-request-id: {requestId})."))
    {
        StatusCode = statusCode;
        RequestId = requestId;
    }

    public HttpStatusCode StatusCode { get; }
    public string? RequestId { get; }
}
