using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal reports that the payer must complete a browser/3DS approval step
/// (e.g. order status PAYER_ACTION_REQUIRED, or a payer-action/approve HATEOAS link).
/// This integration is card-direct only and does not implement an approval round-trip;
/// this is the task-mandated STOP condition.
/// </summary>
public class PayPalActionRequiredException : Exception
{
    public PayPalActionRequiredException(string message) : base(message)
    {
    }
}
