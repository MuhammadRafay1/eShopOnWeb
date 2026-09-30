using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal processed the authorization request but declined the payment
/// (authorization status DENIED) rather than returning an HTTP error.
/// </summary>
public class PaymentDeclinedException : Exception
{
    public PaymentDeclinedException(string reason)
        : base($"PayPal declined the payment: {reason}")
    {
    }
}
