using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a PayPal call fails in a way that is not a clean business decline - a transport
/// failure that survived retries, or an unexpected/error response shape from PayPal. Maps to
/// HTTP 502 Bad Gateway at the API edge (the failure is upstream, not the caller's fault).
/// </summary>
public class PayPalIntegrationException : Exception
{
    public PayPalIntegrationException(string message) : base(message)
    {
    }

    public PayPalIntegrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
