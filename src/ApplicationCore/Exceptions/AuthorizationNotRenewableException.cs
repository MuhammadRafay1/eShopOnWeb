using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a stale PayPal authorization can no longer be renewed (reauthorize failed),
/// so fulfilment cannot proceed. The message is written to be operator-actionable.
/// </summary>
public class AuthorizationNotRenewableException : Exception
{
    public AuthorizationNotRenewableException(string authorizationId, int orderId, string payPalReason)
        : base($"Authorization {authorizationId} for order {orderId} has expired and can no longer be renewed " +
               $"(PayPal: {payPalReason}). Ask the shopper to pay again to create a fresh authorization.")
    {
    }
}
