using System;
using System.Net;

namespace Microsoft.eShopWeb.ApplicationCore.Portfolio;

/// <summary>Base for portfolio errors the API boundary maps to an HTTP status.</summary>
public abstract class PortfolioException : Exception
{
    protected PortfolioException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>The caller's request is invalid (→ 400).</summary>
public sealed class PortfolioValidationException : PortfolioException
{
    public PortfolioValidationException(string message) : base(message) { }
}

/// <summary>A referenced resource does not exist for this caller (→ 404).</summary>
public sealed class PortfolioNotFoundException : PortfolioException
{
    public PortfolioNotFoundException(string message) : base(message) { }
}

/// <summary>The caller is not in a state that permits the action (→ 409).</summary>
public sealed class PortfolioConflictException : PortfolioException
{
    public PortfolioConflictException(string message) : base(message) { }
}

/// <summary>
/// A failure talking to Upvest. Carries the provider HTTP status (when one was received) so the
/// API boundary can distinguish a caller-fixable 4xx from an outage, without leaking provider detail.
/// The message is always caller-safe (never a raw provider body or personal data).
/// </summary>
public sealed class PortfolioProviderException : PortfolioException
{
    public HttpStatusCode? StatusCode { get; }

    public PortfolioProviderException(string message, HttpStatusCode? statusCode, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// A write to Upvest whose outcome could not be confirmed (connection dropped / timed out after the
/// request may have been received). The caller is told the outcome is unknown, never "it failed".
/// </summary>
public sealed class PortfolioUnknownOutcomeException : PortfolioException
{
    public PortfolioUnknownOutcomeException(string message, Exception? inner = null) : base(message, inner) { }
}
