using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The single failure type the Upvest gateway raises. It carries the provider HTTP status (when a
/// response was received) so the API boundary can map provider faults to caller-facing statuses, and
/// distinguishes a caller-fixable rejection from an unknown/transport failure.
///
/// Its <see cref="Exception.Message"/> is caller-safe (it never echoes an SDK message, a URL, or any
/// request body / personal data).
/// </summary>
public class UpvestGatewayException : Exception
{
    /// <summary>The provider HTTP status, or null when nothing answered (transport/timeout).</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// True when the connection failed after the write may already have reached Upvest, so the
    /// outcome is unknown rather than a definite failure.
    /// </summary>
    public bool OutcomeUnknown { get; }

    public UpvestGatewayException(string message, HttpStatusCode? statusCode = null, Exception? inner = null, bool outcomeUnknown = false)
        : base(message, inner)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
    }
}
