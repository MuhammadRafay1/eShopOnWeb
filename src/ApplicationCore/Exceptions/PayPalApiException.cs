using System;
using System.Collections.Generic;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Wraps a non-2xx response from PayPal's API. Carries PayPal's error model
/// (name/message/debug_id/details) so callers can act on it. Never carries card data.
/// </summary>
public class PayPalApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? Name { get; }
    public string? DebugId { get; }
    public IReadOnlyList<string> Details { get; }

    public PayPalApiException(HttpStatusCode statusCode, string? name, string message, string? debugId, IReadOnlyList<string>? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Name = name;
        DebugId = debugId;
        Details = details ?? Array.Empty<string>();
    }
}
