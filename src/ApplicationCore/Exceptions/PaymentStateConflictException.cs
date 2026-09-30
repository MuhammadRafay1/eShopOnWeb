using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a payment/order action is requested from a lifecycle state that does not allow it
/// (e.g. fulfilling an order that was never authorized). Maps to HTTP 409.
/// </summary>
public class PaymentStateConflictException : Exception
{
    public PaymentStateConflictException(string message) : base(message)
    {
    }
}
