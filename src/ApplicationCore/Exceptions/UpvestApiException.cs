using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The single failure type the Upvest integration raises across its boundary. It carries a caller-safe
/// message and, when the provider answered, the HTTP status — so the API layer can map a provider 4xx
/// the caller can act on to the same status, and transport/unknown failures to 5xx, without ever leaking
/// an SDK type name or URL. Never carries personal data.
/// </summary>
public class UpvestApiException : Exception
{
    /// <summary>The provider HTTP status when one was received; null for transport/timeout/config failures.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// True when a write's outcome could not be determined (the connection failed after the request may
    /// have reached Upvest). The caller must treat the result as unknown, not as a definite failure.
    /// </summary>
    public bool OutcomeUnknown { get; }

    public UpvestApiException(string message, HttpStatusCode? statusCode = null, bool outcomeUnknown = false, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
    }

    /// <summary>True when the provider refused the call for lack of the required OAuth scope (401).
    /// In this sandbox that is an environment limit on non-user operations, not a code fault.</summary>
    public bool IsScopeDenied => StatusCode == HttpStatusCode.Unauthorized;
}
