using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown both when a saved card truly does not exist and when it exists but belongs to a different
/// buyer - callers must not be able to distinguish "not yours" from "not found". Maps to HTTP 404.
/// </summary>
public class PaymentMethodNotFoundException : Exception
{
    public PaymentMethodNotFoundException(int paymentMethodId) : base($"No saved card found with id {paymentMethodId}")
    {
    }
}
