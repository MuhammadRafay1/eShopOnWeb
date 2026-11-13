using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The single failure type the Upvest boundary raises. Carries the HTTP status Upvest answered with
/// (when there was one) so callers can tell a caller-fixable 4xx from a provider-side problem, and a
/// caller-safe message — never the provider's raw body or an SDK exception's text.
/// </summary>
public class UpvestIntegrationException : Exception
{
    /// <summary>The HTTP status Upvest returned, or null for a transport/timeout/credential failure.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// True when Upvest could have acted even though the call did not return cleanly (a transport
    /// failure or timeout on a write) — the outcome is unknown and must be reconciled, not reported failed.
    /// </summary>
    public bool OutcomeUnknown { get; }

    /// <summary>
    /// True when the failure is the client's own credentials/scopes being refused with no HTTP status on the
    /// operation — e.g. the OAuth token request for a scope the sandbox client is not authorised for.
    /// </summary>
    public bool CredentialOrScopeRefused { get; }

    public UpvestIntegrationException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null, bool outcomeUnknown = false, bool credentialOrScopeRefused = false)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
        CredentialOrScopeRefused = credentialOrScopeRefused;
    }

    /// <summary>True when the failure is an authorization/scope refusal (the client's own credentials/scopes), not the caller's fault.</summary>
    public bool IsAuthorizationFailure =>
        StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || CredentialOrScopeRefused;
}
