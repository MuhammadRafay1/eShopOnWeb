using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a stale PayPal authorization can no longer be renewed (reauthorize rejected or
/// past the reauthorization window). Operator-actionable: the shopper must pay again.
/// </summary>
public class AuthorizationRenewalFailedException : Exception
{
    public AuthorizationRenewalFailedException(int orderId, string reason)
        : base($"Order {orderId}: the payment authorization has expired and can no longer be renewed ({reason}). Ask the shopper to pay again.")
    {
        OrderId = orderId;
    }

    public int OrderId { get; }
}
