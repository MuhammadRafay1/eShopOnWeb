using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when caller-supplied input fails a domain validation rule. Maps to HTTP 400.
/// </summary>
public class RequestValidationException : Exception
{
    public RequestValidationException(string message) : base(message)
    {
    }
}
