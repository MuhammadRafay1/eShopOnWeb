using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

/// <summary>
/// The single failure type the Upvest gateway raises, so callers have one type to handle instead
/// of the SDK's exception family. Carries the provider HTTP status where one is known, and whether
/// the outcome of a write is <see cref="OutcomeUnknown"/> (the connection failed after the provider
/// may already have acted — settle by re-reading, never report a bare failure).
/// </summary>
public class UpvestGatewayException : Exception
{
    public UpvestGatewayException(string message, HttpStatusCode? statusCode = null, bool outcomeUnknown = false, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
    }

    /// <summary>The provider HTTP status, where the provider answered.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>True when a write's outcome could not be determined (connection failed mid-write).</summary>
    public bool OutcomeUnknown { get; }

    /// <summary>
    /// True when the failure is the sandbox scope wall: the client is not authorised to use the
    /// scopes this operation needs ("This client is not authorised to use these scopes"). This is
    /// an environment limitation, not a code fault — callers keep the record in its pending state.
    /// </summary>
    public bool IsScopeWall { get; init; }
}
