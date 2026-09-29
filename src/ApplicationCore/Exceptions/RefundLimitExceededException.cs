using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a refund would take the total refunded amount beyond what was captured. Enforced
/// locally before ever calling PayPal so the operator gets a clean 4xx. Maps to HTTP 409.
/// </summary>
public class RefundLimitExceededException : Exception
{
    public RefundLimitExceededException(string message) : base(message)
    {
    }
}
