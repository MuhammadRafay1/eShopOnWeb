using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a saved payment method does not exist, or exists but is not owned by the caller. The
/// message never distinguishes the two cases so a shopper can never probe for another shopper's cards.
/// </summary>
public class PaymentMethodNotFoundException : Exception
{
    public PaymentMethodNotFoundException(int paymentMethodId)
        : base($"No payment method found with id {paymentMethodId}")
    {
    }
}
