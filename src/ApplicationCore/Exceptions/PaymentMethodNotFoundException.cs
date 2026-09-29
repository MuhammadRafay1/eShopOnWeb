using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a saved card does not exist, or exists but is not owned by the caller. Used for
/// both cases so the API returns 404 without leaking another shopper's card. Maps to 404.
/// </summary>
public class PaymentMethodNotFoundException : Exception
{
    public PaymentMethodNotFoundException(int paymentMethodId)
        : base($"No saved payment method found with id {paymentMethodId}")
    {
    }
}
