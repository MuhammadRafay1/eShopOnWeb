using System;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;

/// <summary>
/// Raised when a call to Upvest does not succeed. Carries the HTTP status and Upvest's
/// problem-details message so callers can react, but never carries any personal data.
/// </summary>
public class UpvestApiException : Exception
{
    public UpvestApiException(int statusCode, string operation, string? detail)
        : base($"Upvest call '{operation}' failed with status {statusCode}: {detail}")
    {
        StatusCode = statusCode;
        Operation = operation;
        Detail = detail;
    }

    public int StatusCode { get; }
    public string Operation { get; }
    public string? Detail { get; }

    /// <summary>
    /// True when the failure is Upvest refusing the client's scopes — the documented limitation
    /// of this sandbox client (authorised for user operations only). Distinguished so the shop can
    /// report an honest "not available in this environment" rather than a generic error.
    /// </summary>
    public bool IsScopeAuthorizationFailure =>
        StatusCode == 401 && Detail is not null &&
        Detail.Contains("not authorised to use these scopes", StringComparison.OrdinalIgnoreCase);
}
