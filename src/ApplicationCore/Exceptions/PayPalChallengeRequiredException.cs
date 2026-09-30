namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal responds to a card payment with a challenge that requires the shopper to approve
/// in a browser (e.g. 3DS contingency, status PAYER_ACTION_REQUIRED). This integration is synchronous /
/// browser-free by design, so this is surfaced as an error rather than attempted.
/// </summary>
public class PayPalChallengeRequiredException : PayPalException
{
    public PayPalChallengeRequiredException(string message)
        : base(message, 422)
    {
    }
}
