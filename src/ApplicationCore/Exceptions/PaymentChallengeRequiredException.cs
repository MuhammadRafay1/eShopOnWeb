namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// PayPal requires the shopper to approve the payment in a browser (e.g. 3-D Secure / PAYER_ACTION_REQUIRED).
/// This integration is browser-free by design (STOP condition) - surfaced to the caller rather than
/// worked around with an approval round-trip.
/// </summary>
public class PaymentChallengeRequiredException : PaymentException
{
    public PaymentChallengeRequiredException()
        : base("PayPal requires additional shopper authentication (a browser approval step) for this card. " +
               "This integration only supports direct, browser-free card payments; try a different card.")
    {
    }
}
