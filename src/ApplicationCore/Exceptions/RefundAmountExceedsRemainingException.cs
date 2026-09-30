using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a refund would take the total refunded past what was actually captured. Guards the
/// invariant that a partly-refunded order can never become refundable beyond the captured amount.
/// </summary>
public class RefundAmountExceedsRemainingException : Exception
{
    public RefundAmountExceedsRemainingException(decimal requested, decimal remaining)
        : base($"Refund of {requested:0.00} exceeds the remaining refundable amount of {remaining:0.00}.")
    {
        Requested = requested;
        Remaining = remaining;
    }

    public decimal Requested { get; }
    public decimal Remaining { get; }
}
