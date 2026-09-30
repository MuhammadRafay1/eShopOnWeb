using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal answers a card payment with a challenge that requires the shopper
/// to approve in a browser (e.g. a PAYER_ACTION_REQUIRED status or a payer-action link).
/// This integration deliberately does not build a browser approval round-trip — per the
/// task, we STOP and surface this so an operator can see it.
/// </summary>
public class PayPalChallengeRequiredException : Exception
{
    public PayPalChallengeRequiredException(string message) : base(message)
    {
    }
}
