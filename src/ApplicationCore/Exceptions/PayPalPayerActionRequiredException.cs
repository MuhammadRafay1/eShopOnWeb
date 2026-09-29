using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal answers a card payment with a challenge requiring the shopper to approve in a
/// browser (order status PAYER_ACTION_REQUIRED / 3-D Secure). Per the task rules this integration
/// does not build an approval round-trip — it surfaces the condition instead.
/// </summary>
public class PayPalPayerActionRequiredException : Exception
{
    public PayPalPayerActionRequiredException(string payPalOrderId)
        : base($"PayPal requires payer action (3-D Secure / browser approval) for order {payPalOrderId}. " +
               "This integration does not support a browser approval round-trip; use a card that does not trigger a challenge.")
    {
    }
}
