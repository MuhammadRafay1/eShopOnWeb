using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a requested refund amount would push the order's total refunded amount past what was
/// captured. Enforced before any PayPal call is made. Maps to HTTP 422.
/// </summary>
public class RefundAmountExceededException : Exception
{
    public RefundAmountExceededException(string message) : base(message)
    {
    }
}
