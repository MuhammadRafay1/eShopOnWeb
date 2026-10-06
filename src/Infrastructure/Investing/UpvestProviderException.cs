using System;
using System.Net;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// The single error type the Upvest integration boundary raises. Every SDK failure — a typed API error,
/// a connection/timeout failure, a drifted response, or an auth-scheme failure — is translated to this
/// so callers handle one failure type. Carries the HTTP status where one is available. Messages are
/// caller-safe and never contain personal data, secrets, or raw provider bodies.
/// </summary>
public sealed class UpvestProviderException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// True when a write may have reached the provider but its outcome is unknown (connection/timeout
    /// failure). The caller must reconcile rather than treat the write as definitely failed.
    /// </summary>
    public bool OutcomeUnknown { get; init; }

    public UpvestProviderException(string message, HttpStatusCode? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
