using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The requested refund, combined with any refunds already issued against the capture,
/// would exceed the amount PayPal actually captured for the order.
/// </summary>
public class RefundExceedsCapturedAmountException : Exception
{
    public RefundExceedsCapturedAmountException(string message) : base(message)
    {
    }
}
