using System;
using System.Net;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;

/// <summary>
/// The single failure type the Upvest gateway raises. It collapses the SDK's exception family into one type the
/// rest of the application can reason about, carrying the HTTP status (when the provider answered) and flags that
/// separate a caller-fixable rejection, an unauthorised-scope condition, and an outcome that is genuinely unknown
/// after a transport failure. It never carries personal data or secrets.
/// </summary>
public sealed class UpvestProviderException : Exception
{
    public UpvestProviderException(string message, HttpStatusCode? statusCode = null, bool outcomeUnknown = false,
        bool unauthorizedScope = false, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
        UnauthorizedScope = unauthorizedScope;
    }

    /// <summary>HTTP status the provider returned, or null when nothing usable answered (transport failure/timeout).</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// True when a write's connection failed after the provider may already have acted, so the outcome could not be settled.
    /// </summary>
    public bool OutcomeUnknown { get; }

    /// <summary>
    /// True when the provider refused the call because this sandbox client is not authorised for the required scope
    /// (401 "This client is not authorised to use these scopes"). This is an environment limitation, not a caller error.
    /// </summary>
    public bool UnauthorizedScope { get; }
}
