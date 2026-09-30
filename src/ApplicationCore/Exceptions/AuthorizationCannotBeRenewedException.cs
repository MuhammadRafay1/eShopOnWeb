using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown at fulfilment when an order's payment hold has expired and can no longer be renewed with PayPal.
/// The order is reverted to awaiting-payment so the shopper can pay it again. The message is phrased so an
/// operator knows exactly what to do. Maps to HTTP 409.
/// </summary>
public class AuthorizationCannotBeRenewedException : Exception
{
    public AuthorizationCannotBeRenewedException(int orderId)
        : base($"Order {orderId}'s payment hold could not be renewed and must be paid again before it can be fulfilled.")
    {
    }
}
