using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A payment-flow error that maps to a specific HTTP status at the API boundary. The message is
/// safe to return to the caller.
/// </summary>
public class PaymentException : Exception
{
    public PaymentException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public PaymentException(string message, int statusCode, Exception inner) : base(message, inner)
    {
        StatusCode = statusCode;
    }

    /// <summary>The HTTP status the API should return for this error.</summary>
    public int StatusCode { get; }
}

/// <summary>
/// The caller is acting on a resource that does not exist (or is not theirs, surfaced as "not found"
/// so one shopper cannot probe another's data).
/// </summary>
public class PaymentNotFoundException : PaymentException
{
    public PaymentNotFoundException(string message) : base(message, 404) { }
}

/// <summary>The requested operation is not valid for the payment's current state.</summary>
public class PaymentConflictException : PaymentException
{
    public PaymentConflictException(string message) : base(message, 409) { }
}

/// <summary>The caller's request is invalid (e.g. a refund larger than the remaining captured amount).</summary>
public class PaymentValidationException : PaymentException
{
    public PaymentValidationException(string message) : base(message, 422) { }
}

/// <summary>
/// PayPal answered a card payment with a challenge that needs a shopper to approve in a browser.
/// We stop and report it rather than building an approval round-trip.
/// </summary>
public class PaymentChallengeRequiredException : PaymentException
{
    public PaymentChallengeRequiredException(string message)
        : base(message, 402) { }
}
