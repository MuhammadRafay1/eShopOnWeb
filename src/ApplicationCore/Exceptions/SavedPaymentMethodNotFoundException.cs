using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class SavedPaymentMethodNotFoundException : Exception
{
    public SavedPaymentMethodNotFoundException(int paymentMethodId) : base($"No saved payment method found with id {paymentMethodId}")
    {
    }
}
