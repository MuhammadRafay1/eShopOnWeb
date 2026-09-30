using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown at fulfilment when a stale authorization can no longer be renewed (voided/denied/already
/// captured, or PayPal rejected the reauthorization). The message is operator-actionable. Maps to
/// HTTP 409 at the API boundary.
/// </summary>
public class PaymentAuthorizationNotRenewableException : Exception
{
    public PaymentAuthorizationNotRenewableException(string reason) : base(reason)
    {
    }
}
