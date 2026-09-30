using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal could not be reached, or returned an error this integration does not treat as
/// a caller-actionable rejection (transport failure, unrecognized/5xx error, malformed response).
/// Maps to HTTP 502.
/// </summary>
public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
