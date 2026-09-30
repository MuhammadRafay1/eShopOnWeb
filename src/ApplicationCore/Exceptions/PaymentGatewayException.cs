using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown by the PayPal gateway boundary for a PayPal-side problem that is not a deterministic client
/// rejection — a 5xx from PayPal, a connection failure, or an unreadable success body. Distinct from
/// <see cref="PaymentDeclinedException"/> so it never gets folded into a generic 500 that would invite a
/// caller to retry a request that can succeed. Maps to HTTP 502.
/// </summary>
public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
