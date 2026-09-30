using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Wraps an error reported by PayPal. Never carries card data or credentials -
/// only the PayPal error name/message/debug_id/issue needed to act on the failure.
/// </summary>
public class PayPalGatewayException : Exception
{
    public PayPalGatewayException(string message, string? debugId = null, string? issue = null, int? statusCode = null)
        : base(message)
    {
        DebugId = debugId;
        Issue = issue;
        StatusCode = statusCode;
    }

    public string? DebugId { get; }
    public string? Issue { get; }
    public int? StatusCode { get; }
}
