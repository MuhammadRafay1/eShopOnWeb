using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A failure raised by the payment gateway (PayPal). The gateway translates every SDK failure into
/// this single type so the rest of the code has one thing to handle. It carries just enough to map
/// to a caller-facing status without leaking provider internals.
/// </summary>
public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message, int? providerStatusCode, bool isCallerError,
        string? payPalIssue = null, string? payPalDebugId = null, Exception? inner = null)
        : base(message, inner)
    {
        ProviderStatusCode = providerStatusCode;
        IsCallerError = isCallerError;
        PayPalIssue = payPalIssue;
        PayPalDebugId = payPalDebugId;
    }

    /// <summary>The HTTP status PayPal returned, when there was one.</summary>
    public int? ProviderStatusCode { get; }

    /// <summary>True when the caller's input was at fault (a 4xx we can pass back); false for our/credential/transport faults.</summary>
    public bool IsCallerError { get; }

    /// <summary>PayPal's fine-grained issue code (e.g. AUTHORIZATION_EXPIRED), when available.</summary>
    public string? PayPalIssue { get; }

    /// <summary>PayPal's correlation id (debug_id), for our logs.</summary>
    public string? PayPalDebugId { get; }

    /// <summary>True when the connection failed after PayPal may already have acted — the outcome is unknown.</summary>
    public bool OutcomeUnknown { get; init; }
}
