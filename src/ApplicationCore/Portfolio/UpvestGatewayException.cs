using System;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>
/// The single failure type the portfolio gateway raises. The web layer maps it to a caller-facing status.
/// Carries only caller-safe information — never a provider error body that could contain personal data, and
/// never a credential or signing detail.
/// </summary>
public sealed class UpvestGatewayException : Exception
{
    public UpvestGatewayException(
        string message,
        int? statusCode = null,
        bool isAuthorizationScopeIssue = false,
        bool isOutcomeUnknown = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        IsAuthorizationScopeIssue = isAuthorizationScopeIssue;
        IsOutcomeUnknown = isOutcomeUnknown;
    }

    /// <summary>The HTTP status Upvest returned, when the provider answered.</summary>
    public int? StatusCode { get; }

    /// <summary>
    /// True when the failure is this application's own credential lacking a required scope (Upvest 401/403).
    /// Not the caller's fault — the web layer surfaces it as 502, never passes it through.
    /// </summary>
    public bool IsAuthorizationScopeIssue { get; }

    /// <summary>True when a write may or may not have reached Upvest (connection/timeout after send).</summary>
    public bool IsOutcomeUnknown { get; }
}
