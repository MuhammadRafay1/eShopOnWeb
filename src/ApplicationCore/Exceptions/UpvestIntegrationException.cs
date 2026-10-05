using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The single failure type the Upvest gateway raises. Carries a caller-safe message and, where the
/// provider answered, the HTTP status — never the provider's raw body or any personal data.
/// </summary>
public class UpvestIntegrationException : Exception
{
    public UpvestIntegrationException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>The provider's HTTP status, when the provider answered; otherwise null (transport failure).</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>True when the provider rejected the caller's input (a 4xx that the caller could act on).</summary>
    public bool IsClientError =>
        StatusCode is { } s && (int)s is >= 400 and < 500 && s != HttpStatusCode.TooManyRequests
        && s != HttpStatusCode.Unauthorized && s != HttpStatusCode.Forbidden;
}
