using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a saved card does not exist, or exists but does not belong to the calling shopper.
/// Ownership mismatches are surfaced as "not found" so one shopper cannot probe for another's cards.
/// </summary>
public class PaymentMethodNotFoundException : Exception
{
    public PaymentMethodNotFoundException(int paymentMethodId)
        : base($"Payment method {paymentMethodId} was not found.")
    {
    }
}
