using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class PaymentMethodNotFoundException : Exception
{
    public PaymentMethodNotFoundException(string message) : base(message)
    {
    }
}
