using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a refund would take the total refunded amount beyond what was actually captured.
/// </summary>
public class RefundExceedsCaptureException : Exception
{
    public RefundExceedsCaptureException(decimal requested, decimal alreadyRefunded, decimal captured)
        : base($"Refund of {requested:0.00} would exceed the captured amount. " +
               $"Captured: {captured:0.00}, already refunded: {alreadyRefunded:0.00}, " +
               $"remaining refundable: {(captured - alreadyRefunded):0.00}.")
    {
    }
}
