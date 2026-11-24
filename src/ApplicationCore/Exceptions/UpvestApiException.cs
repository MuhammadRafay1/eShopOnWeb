using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Raised when a call to the Upvest API fails. Carries the upstream HTTP status and a sanitised
/// problem detail (never any personal data) so it can be surfaced to operators without leaking PII.
/// </summary>
public class UpvestApiException : Exception
{
    public int StatusCode { get; }
    public string? UpstreamDetail { get; }

    public UpvestApiException(string operation, int statusCode, string? upstreamDetail)
        : base($"Upvest call '{operation}' failed with status {statusCode}: {upstreamDetail}")
    {
        StatusCode = statusCode;
        UpstreamDetail = upstreamDetail;
    }

    public UpvestApiException(string operation, Exception inner)
        : base($"Upvest call '{operation}' failed: {inner.Message}", inner)
    {
        StatusCode = 0;
    }
}
