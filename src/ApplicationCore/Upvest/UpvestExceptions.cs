using System;

namespace Microsoft.eShopWeb.ApplicationCore.Upvest;

/// <summary>
/// A call to Upvest failed. The message is safe to log: it never contains personal data,
/// secrets, or request bodies — only the HTTP status, the Upvest error type, and its request id.
/// </summary>
public class UpvestApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorType { get; }
    public string? RequestId { get; }

    public UpvestApiException(int statusCode, string? errorType, string? requestId, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
        RequestId = requestId;
    }
}

/// <summary>
/// Upvest refused the call because this client is not authorised for the required scope. In this
/// sandbox that is expected for every non-user operation; callers may degrade gracefully.
/// </summary>
public class UpvestAuthorizationException : UpvestApiException
{
    public UpvestAuthorizationException(int statusCode, string? errorType, string? requestId, string message)
        : base(statusCode, errorType, requestId, message)
    {
    }
}
