using System;
using System.Net;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Raised when a call to Upvest returns a non-success status. Carries the status code and a
/// sanitised message (Upvest <c>problem+json</c> detail) so callers can map outcomes without
/// leaking request/response bodies that may contain personal data.
/// </summary>
public class UpvestApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public UpvestApiException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
