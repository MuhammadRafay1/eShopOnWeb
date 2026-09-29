using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal declines a payment operation for a business reason (a declined card, a
/// failed capture, etc.) rather than a transient/infrastructure fault. Carries PayPal's own
/// issue text so the caller can act on it. Maps to HTTP 402 Payment Required at the API edge.
/// </summary>
public class PaymentDeclinedException : Exception
{
    public PaymentDeclinedException(string message) : base(message)
    {
    }
}
