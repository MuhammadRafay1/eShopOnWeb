namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The order is not in a state that allows the requested payment action (e.g. paying an
/// already-authorized order, fulfilling an unauthorized order).
/// </summary>
public class PaymentAuthorizationException : PaymentException
{
    public PaymentAuthorizationException(string message) : base(message)
    {
    }
}
