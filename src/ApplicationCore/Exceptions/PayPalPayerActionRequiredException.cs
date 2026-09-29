using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal answers a card payment or vault save with a challenge that requires the
/// shopper to approve in a browser (order/setup-token status <c>PAYER_ACTION_REQUIRED</c>, or a
/// <c>payer-action</c> HATEOAS link). Per the task this is a STOP-and-report condition: it must
/// not be swallowed by generic error handling or turned into an approval round-trip. Propagates as
/// a distinct, loud failure.
/// </summary>
public class PayPalPayerActionRequiredException : Exception
{
    public PayPalPayerActionRequiredException(string context)
        : base($"PayPal requires payer action (browser approval) for {context}. " +
               "This integration is server-side only and does not implement an approval round-trip.")
    {
    }
}
