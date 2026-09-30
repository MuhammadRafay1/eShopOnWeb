using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class RefundExceedsCapturedException : Exception
{
    public RefundExceedsCapturedException(decimal requested, decimal remaining)
        : base($"Refund amount {requested:0.00} exceeds the refundable remaining balance of {remaining:0.00}.")
    {
    }
}
