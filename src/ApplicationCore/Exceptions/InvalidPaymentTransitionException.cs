using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class InvalidPaymentTransitionException : Exception
{
    public InvalidPaymentTransitionException(string currentStatus, string attemptedAction)
        : base($"Cannot {attemptedAction} an order in status '{currentStatus}'.")
    {
    }
}
