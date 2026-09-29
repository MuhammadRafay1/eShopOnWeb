using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal returns a non-2xx response. Carries PayPal's own error envelope
/// (<c>name</c>/<c>message</c>/<c>debug_id</c>/<c>details</c>) so it can be logged and, for
/// operator-facing endpoints, surfaced. Never surface <see cref="Exception.Message"/> raw to a
/// shopper. Maps to 502 by default.
/// </summary>
public class PayPalApiException : Exception
{
    public int HttpStatus { get; }
    public string? Name { get; }
    public string? DebugId { get; }
    public string? Details { get; }

    public PayPalApiException(int httpStatus, string? name, string message, string? debugId, string? details)
        : base(message)
    {
        HttpStatus = httpStatus;
        Name = name;
        DebugId = debugId;
        Details = details;
    }
}
