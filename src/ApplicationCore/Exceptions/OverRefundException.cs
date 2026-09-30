using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class OverRefundException : Exception
{
    public OverRefundException(int orderId, decimal capturedAmount, decimal alreadyRefunded, decimal requestedAmount)
        : base($"Order {orderId}: refund of {requestedAmount} would exceed the captured amount of {capturedAmount} (already refunded {alreadyRefunded}).")
    {
    }
}
