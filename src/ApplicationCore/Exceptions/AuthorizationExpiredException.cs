namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The order's PayPal authorization has gone stale and could not be renewed. Operator-actionable:
/// the order needs to be re-paid (a fresh authorization) before it can be fulfilled.
/// </summary>
public class AuthorizationExpiredException : PaymentException
{
    public AuthorizationExpiredException(int orderId)
        : base($"Order {orderId}'s PayPal authorization has expired and can no longer be renewed. " +
               "The shopper must pay the order again (POST /api/orders/{orderId}/pay) before it can be fulfilled.")
    {
    }
}
