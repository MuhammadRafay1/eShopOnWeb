using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class PaymentGatewayException : Exception
{
    public int? HttpStatusCode { get; }
    public string? DebugId { get; }

    public PaymentGatewayException(string message, int? httpStatusCode = null, string? debugId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        HttpStatusCode = httpStatusCode;
        DebugId = debugId;
    }
}
