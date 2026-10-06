using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// Raised by <see cref="IUpvestGateway"/> when a call to the investment provider fails. Carries a
/// caller-safe message and, where the provider answered, the HTTP status — never any personal data.
/// </summary>
public sealed class UpvestGatewayException : Exception
{
    public UpvestGatewayException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>The provider's HTTP status, when it answered; null for transport/timeout failures.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>True when the provider may have acted but the outcome could not be confirmed.</summary>
    public bool OutcomeUnknown { get; init; }
}
