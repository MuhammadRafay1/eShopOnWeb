using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Raised when a billing operation cannot be completed because of an issue
/// with the request itself (e.g. an unknown plan handle). Carries the HTTP
/// status code the API layer should surface.
/// </summary>
public class BillingException : Exception
{
    public int StatusCode { get; }

    public BillingException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}