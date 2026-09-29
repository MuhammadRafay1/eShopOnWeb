using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// An authorization has gone stale before fulfilment and can no longer be renewed (reauthorized).
/// This is a documented, recoverable-by-repayment condition, not an unexpected upstream failure, so
/// it maps to 409 with an operator-actionable body (code + PayPal's message + guidance), not a 502.
/// </summary>
public class AuthorizationUnrenewableException : Exception
{
    public const string ErrorCode = "AUTHORIZATION_EXPIRED_UNRENEWABLE";

    public string Guidance { get; }

    public AuthorizationUnrenewableException(string payPalMessage)
        : base(payPalMessage)
    {
        Guidance = "This hold can no longer be renewed. The shopper must pay again via " +
                   "POST /api/orders/{orderId}/pay before this order can be fulfilled.";
    }
}
