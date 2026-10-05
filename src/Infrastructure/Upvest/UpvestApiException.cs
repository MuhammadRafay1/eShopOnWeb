using System;
using System.Net;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Raised when Upvest returns a non-success response. Carries only the status code and the error
/// "type"; response bodies (which can echo personal data) are never included.
/// </summary>
public sealed class UpvestApiException : Exception
{
    public UpvestApiException(HttpStatusCode statusCode, string operation, string? errorType)
        : base($"Upvest call '{operation}' failed with status {(int)statusCode} ({errorType ?? "unknown"}).")
    {
        StatusCode = statusCode;
        ErrorType = errorType;
    }

    public HttpStatusCode StatusCode { get; }
    public string? ErrorType { get; }
}
