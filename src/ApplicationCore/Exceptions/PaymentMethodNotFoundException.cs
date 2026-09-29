using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a saved card does not exist OR belongs to a different shopper. Same exception for
/// both cases so the API maps both to 404 without leaking existence.
/// </summary>
public class PaymentMethodNotFoundException : Exception
{
    public PaymentMethodNotFoundException(int paymentMethodId) : base($"No payment method found with id {paymentMethodId}")
    {
    }
}
