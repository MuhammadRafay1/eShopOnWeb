using System;

namespace Microsoft.eShopWeb.ApplicationCore.Upvest;

/// <summary>
/// Raised when a call to Upvest fails. Carries the HTTP status so callers can tell an
/// authorization problem (e.g. a scope the sandbox client does not have) from other failures.
/// The message is safe to surface and never contains personal details.
/// </summary>
public class UpvestApiException : Exception
{
    public UpvestApiException(int statusCode, string operation, string? detail = null)
        : base($"Upvest call '{operation}' failed with status {statusCode}" +
               (string.IsNullOrEmpty(detail) ? "." : $": {detail}"))
    {
        StatusCode = statusCode;
        Operation = operation;
    }

    public int StatusCode { get; }
    public string Operation { get; }

    /// <summary>True when Upvest refused the call for lack of an authorised scope.</summary>
    public bool IsAuthorizationError => StatusCode is 401 or 403;
}
