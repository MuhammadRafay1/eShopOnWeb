using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal answers a direct card payment by asking for a shopper-facing browser
/// approval/challenge (order status PAYER_ACTION_REQUIRED or an equivalent authorization
/// review reason). The task's contract says this must be a hard stop for the sandbox test
/// card - it is surfaced distinctly rather than treated as a recoverable decline or built out
/// into an approval round-trip. Maps to HTTP 409 Conflict at the API edge.
/// </summary>
public class PayPalApprovalRequiredException : Exception
{
    public PayPalApprovalRequiredException(string message) : base(message)
    {
    }
}
