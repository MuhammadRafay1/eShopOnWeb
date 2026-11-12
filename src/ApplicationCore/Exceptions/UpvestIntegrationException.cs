using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// How a failed Upvest call should be presented to the caller. Keeps "the caller sent something invalid"
/// distinct from "the provider (or our credentials/quota) is the problem".
/// </summary>
public enum UpvestFailureKind
{
    /// <summary>The provider rejected the caller's request (a 4xx the caller can act on).</summary>
    CallerError = 0,

    /// <summary>Our credentials or scopes were refused, or our quota is spent — not the caller's fault.</summary>
    ProviderRejectedUs = 1,

    /// <summary>Transport failure, timeout, or a provider 5xx — no meaningful caller status.</summary>
    ProviderUnavailable = 2,

    /// <summary>A write whose outcome could not be settled — it may or may not have landed.</summary>
    Unknown = 3
}

/// <summary>
/// The single failure type the portfolio endpoints handle. Carries the provider HTTP status (when one was
/// seen) and a <see cref="UpvestFailureKind"/> the endpoint maps to a caller-facing status. Messages are
/// caller-safe and never carry personal data or secrets.
/// </summary>
public class UpvestIntegrationException : Exception
{
    public UpvestIntegrationException(string message, UpvestFailureKind kind, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
    }

    public UpvestFailureKind Kind { get; }

    /// <summary>The provider HTTP status, when the provider answered; null for a transport failure.</summary>
    public int? StatusCode { get; }
}
