using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal rejects an operation or is unreachable. Carries PayPal's own error name and
/// debug id (when the failure is a typed API rejection) so an operator can act on it. Maps to
/// HTTP 502 at the API boundary.
/// </summary>
public class PaymentGatewayException : Exception
{
    public string? PayPalErrorName { get; }
    public string? PayPalDebugId { get; }

    public PaymentGatewayException(string message, string? payPalErrorName = null, string? payPalDebugId = null)
        : base(message)
    {
        PayPalErrorName = payPalErrorName;
        PayPalDebugId = payPalDebugId;
    }

    public PaymentGatewayException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
