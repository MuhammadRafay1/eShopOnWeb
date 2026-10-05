using System;

namespace Microsoft.eShopWeb.Infrastructure.Billing.MaxioApi;

/// <summary>
/// Thrown when the Maxio Billing API rejects or fails a request in an
/// unexpected way. The status code and error body are preserved for logging;
/// the API key is never included.
/// </summary>
public sealed class MaxioApiException : Exception
{
    public int StatusCode { get; }

    public string? ErrorBody { get; }

    public MaxioApiException(int statusCode, string? errorBody)
        : base($"Maxio Billing API request failed (HTTP {statusCode}): {errorBody ?? "(no response body)"}")
    {
        StatusCode = statusCode;
        ErrorBody = errorBody;
    }
}