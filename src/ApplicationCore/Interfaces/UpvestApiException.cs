using System;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Raised when Upvest returns a non-success response. Carries the HTTP status and Upvest's
/// problem <c>type</c>/<c>detail</c> so callers can react, without exposing request bodies.
/// </summary>
public class UpvestApiException : Exception
{
    public int StatusCode { get; }
    public string? ProblemType { get; }

    public UpvestApiException(int statusCode, string? problemType, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ProblemType = problemType;
    }

    /// <summary>
    /// True when the failure is the sandbox refusing an unauthorised scope
    /// ("This client is not authorised to use these scopes"), i.e. an environment limit, not a bug.
    /// </summary>
    public bool IsNotAuthorisedScope => StatusCode == 401;
}
