using System;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The single failure type the Upvest gateway surfaces. The gateway converts every SDK failure — API errors,
/// connection/timeout failures, deserialization failures and credential failures — into this type, so callers
/// have one thing to handle. Messages are caller-safe and never carry personal details or secrets.
/// </summary>
public sealed class UpvestGatewayException : Exception
{
    public UpvestGatewayException(string message, int? statusCode = null, bool outcomeUnknown = false, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        OutcomeUnknown = outcomeUnknown;
    }

    /// <summary>The provider HTTP status, when the provider answered.</summary>
    public int? StatusCode { get; }

    /// <summary>
    /// True when a write may have reached Upvest despite the failure (a connection/timeout fault after send),
    /// so the caller must reconcile rather than treat it as a definite failure.
    /// </summary>
    public bool OutcomeUnknown { get; }
}
