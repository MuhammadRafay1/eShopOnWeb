using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Base type for failures talking to Upvest.</summary>
public abstract class UpvestException : Exception
{
    protected UpvestException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Upvest answered with an error status. Carries the HTTP status so callers can tell apart caller faults from provider faults.</summary>
public sealed class UpvestApiException : UpvestException
{
    public int StatusCode { get; }

    public UpvestApiException(string operation, int statusCode, string? detail, Exception? inner = null)
        : base($"Upvest operation '{operation}' failed with status {statusCode}.{(string.IsNullOrEmpty(detail) ? "" : " " + detail)}", inner)
    {
        StatusCode = statusCode;
    }
}

/// <summary>No usable response from Upvest (connection/timeout). The outcome of a write may be unknown.</summary>
public sealed class UpvestUnavailableException : UpvestException
{
    public UpvestUnavailableException(string operation, Exception? inner = null)
        : base($"Upvest operation '{operation}' did not get a usable response.", inner) { }
}
