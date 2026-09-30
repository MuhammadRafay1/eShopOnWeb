using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal answers a card payment with a challenge that requires the shopper to approve in a
/// browser (3DS / payer action). This integration is deliberately no-redirect, so it stops here rather than
/// building an approval round-trip. Maps to HTTP 402.
/// </summary>
public class PayerActionRequiredException : Exception
{
    public PayerActionRequiredException(int orderId)
        : base($"PayPal requires the shopper to approve this payment in a browser (3DS challenge) for order " +
               $"{orderId}. This integration does not support a browser approval step.")
    {
    }
}
