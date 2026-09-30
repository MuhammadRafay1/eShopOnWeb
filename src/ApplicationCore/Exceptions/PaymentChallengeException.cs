using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// PayPal answered a card payment (or a vault setup token) with a challenge that requires the
/// shopper to approve in a browser (order status PAYER_ACTION_REQUIRED, a "payer-action" link,
/// or a 3DS "C" authentication status). This integration is direct-card, headless, and does not
/// implement an approval round-trip, so this condition is surfaced as an error rather than
/// silently handled.
/// </summary>
public class PaymentChallengeException : Exception
{
    public PaymentChallengeException(string message) : base(message)
    {
    }
}
