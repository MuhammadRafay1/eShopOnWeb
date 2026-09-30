using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal requires a browser-based payer approval (e.g. 3DS challenge) to
/// proceed. This integration is server-to-server only, so such a payment is stopped and
/// reported rather than driven through an approval round-trip.
/// </summary>
public class PaymentChallengeRequiredException : Exception
{
    public PaymentChallengeRequiredException(string detail)
        : base($"PayPal requires payer approval in a browser to complete this payment, which this API cannot perform: {detail}")
    {
    }
}
