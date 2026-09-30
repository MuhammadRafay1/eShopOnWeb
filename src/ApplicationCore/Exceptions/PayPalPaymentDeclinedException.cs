using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal declines a card payment (the authorization was not created). Carries PayPal's
/// issue/description so the shopper/operator learns why. This is a normal, shopper-actionable
/// outcome (e.g. try a different card), distinct from the "STOP and report" SCA-challenge case.
/// </summary>
public class PayPalPaymentDeclinedException : Exception
{
    public PayPalPaymentDeclinedException(string message) : base(message)
    {
    }
}
