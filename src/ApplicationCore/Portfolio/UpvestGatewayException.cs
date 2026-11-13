using System;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>
/// Raised by the Upvest gateway when a call to Upvest fails. Carries a caller-safe message and,
/// where Upvest answered, the HTTP status. Never carries personal data or a credential.
/// </summary>
public sealed class UpvestGatewayException : Exception
{
    public UpvestGatewayException(string message, int? statusCode, bool outcomeUnknown, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
    }

    /// <summary>The HTTP status Upvest returned, when it answered; null for a transport failure.</summary>
    public int? StatusCode { get; }

    /// <summary>
    /// True when the call failed in a way that leaves the outcome at Upvest genuinely unknown
    /// (a transport failure or timeout after the request may have been received). A write with an
    /// unknown outcome must be reconciled by re-reading provider state, not reported as failed.
    /// </summary>
    public bool OutcomeUnknown { get; }

    /// <summary>
    /// True when the failure is an authorisation/permission problem at Upvest (this client's
    /// credentials or scopes), not something the shopper did — e.g. the sandbox's user-only client
    /// refused with 401 "not authorised to use these scopes".
    /// </summary>
    public bool IsAuthorizationFailure => StatusCode is 401 or 403;
}
