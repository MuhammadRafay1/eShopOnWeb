using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal answers a card payment with a challenge that requires the shopper to approve
/// in a browser (e.g. 3-D Secure / <c>PAYER_ACTION_REQUIRED</c>). Per the task, this is a
/// "STOP and report" condition — this integration deliberately does NOT build a browser approval
/// round-trip. It should not occur for a sandbox account provisioned for direct card processing
/// with the standard test card; if it does, it is something to escalate.
/// </summary>
public class PayPalChallengeRequiredException : Exception
{
    public PayPalChallengeRequiredException(string message) : base(message)
    {
    }
}
