using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The targeted resource exists but is in the wrong state for the requested action — e.g. paying an
/// already-authorized order, fulfilling a non-authorized order, cancelling a fulfilled one, or an
/// idempotency short-circuit where the action has already happened. Maps to 409 Conflict.
/// </summary>
public class PaymentConflictException : Exception
{
    public PaymentConflictException(string message) : base(message)
    {
    }
}
