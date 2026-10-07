using System;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// A provider-neutral wrapper around a failed Upvest call. Carries the operation name and transport status
/// only; the original SDK exception is kept as the inner exception for diagnostics but is not surfaced to
/// callers or logs (its body could echo personal data).
/// </summary>
public class UpvestGatewayException : Exception
{
    public UpvestGatewayException(string operation, int statusCode, Exception inner)
        : base($"Upvest call '{operation}' failed with status {statusCode}.", inner)
    {
        Operation = operation;
        StatusCode = statusCode;
    }

    public string Operation { get; }
    public int StatusCode { get; }
}
