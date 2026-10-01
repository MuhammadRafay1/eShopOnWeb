using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a payment operation is attempted while the OrderPayment is in a status that does
/// not allow it (e.g. fulfilling an order that was never authorized). Maps to HTTP 409.
/// </summary>
public class OrderPaymentStateException : Exception
{
    public OrderPaymentStateException(string message) : base(message)
    {
    }
}
