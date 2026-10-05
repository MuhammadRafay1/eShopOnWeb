using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A failure talking to Upvest, translated at the gateway boundary so the rest of the application has a
/// single failure type. Carries the HTTP status where one is known, and a flag for writes whose outcome
/// could not be determined (the connection failed after the request may have reached Upvest).
/// </summary>
public class UpvestApiException : Exception
{
    public UpvestApiException(string message, int? statusCode = null, bool outcomeUnknown = false, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
    }

    /// <summary>The HTTP status Upvest returned, when the server answered.</summary>
    public int? StatusCode { get; }

    /// <summary>True when a write may or may not have taken effect and must be reconciled.</summary>
    public bool OutcomeUnknown { get; }
}
