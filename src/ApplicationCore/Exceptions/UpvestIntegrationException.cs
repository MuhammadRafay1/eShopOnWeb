using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The single failure type the Upvest gateway raises for every provider failure, so callers have one type to
/// handle instead of the SDK's family. Carries the provider HTTP status (when one exists) so the API boundary
/// can map it to a caller-facing status, and an <see cref="OutcomeUnknown"/> flag for a write whose connection
/// failed after the provider may already have acted.
/// </summary>
public class UpvestIntegrationException : Exception
{
    public UpvestIntegrationException(string message, HttpStatusCode? statusCode = null, bool outcomeUnknown = false, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
    }

    /// <summary>The provider HTTP status, when the provider answered; null when nothing usable answered.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>True when a write's outcome could not be settled (connection failure after a possible effect).</summary>
    public bool OutcomeUnknown { get; }
}
