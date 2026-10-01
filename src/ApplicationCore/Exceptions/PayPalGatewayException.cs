using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// PayPal rejected a call, or the call's outcome could not be determined (connection/timeout).
/// Carries only caller-safe information - never raw SDK exception text.
/// </summary>
public class PayPalGatewayException : PaymentException
{
    /// <summary>HTTP status PayPal returned, where known (null for a connection/timeout failure).</summary>
    public int? StatusCode { get; }

    /// <summary>PayPal's own correlation id for this failure (Error.DebugId), for support/log correlation.</summary>
    public string? DebugId { get; }

    /// <summary>True when the call's outcome is unknown (e.g. a transport failure) rather than a definite rejection.</summary>
    public bool IsOutcomeUnknown { get; }

    public PayPalGatewayException(string message, int? statusCode, string? debugId, bool isOutcomeUnknown = false)
        : base(message)
    {
        StatusCode = statusCode;
        DebugId = debugId;
        IsOutcomeUnknown = isOutcomeUnknown;
    }

    public PayPalGatewayException(string message, Exception innerException, bool isOutcomeUnknown = true)
        : base(message, innerException)
    {
        IsOutcomeUnknown = isOutcomeUnknown;
    }
}
