using System;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

/// <summary>
/// Raised when Upvest returns a non-success response. Carries the HTTP status code and the
/// (non-personal) error detail Upvest reported, so endpoints can surface an honest outcome
/// without leaking any request data.
/// </summary>
public class UpvestException : Exception
{
    public int StatusCode { get; }

    /// <summary>True when Upvest refused the call because this client lacks the required scope.</summary>
    public bool IsScopeAuthorizationFailure { get; }

    public UpvestException(int statusCode, string message, bool isScopeAuthorizationFailure = false)
        : base(message)
    {
        StatusCode = statusCode;
        IsScopeAuthorizationFailure = isScopeAuthorizationFailure;
    }
}
