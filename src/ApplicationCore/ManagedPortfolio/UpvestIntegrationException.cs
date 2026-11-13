using System;

namespace Microsoft.eShopWeb.ApplicationCore.ManagedPortfolio;

/// <summary>
/// How a failed Upvest interaction should surface to the API caller.
/// </summary>
public enum UpvestFailureKind
{
    /// <summary>The caller sent something the provider rejected (a 4xx). Surface as that client status.</summary>
    InvalidRequest,

    /// <summary>
    /// Our credentials/scope, our quota, the provider being down, a transport fault, or an unreadable
    /// response. Never the caller's fault — surface as a 5xx.
    /// </summary>
    ProviderUnavailable
}

/// <summary>
/// The single application-level failure type the provider layer raises. It carries a caller-safe message and
/// the status the boundary should return; the real cause (SDK exception, provider body) stays in
/// <see cref="Exception.InnerException"/> for logs and never reaches the caller. Personal data is never placed
/// in <see cref="Exception.Message"/>.
/// </summary>
public sealed class UpvestIntegrationException : Exception
{
    public UpvestFailureKind Kind { get; }

    /// <summary>The provider HTTP status, when one was received; null for transport/credential failures.</summary>
    public int? ProviderStatusCode { get; }

    public UpvestIntegrationException(
        string callerSafeMessage,
        UpvestFailureKind kind,
        int? providerStatusCode = null,
        Exception? innerException = null)
        : base(callerSafeMessage, innerException)
    {
        Kind = kind;
        ProviderStatusCode = providerStatusCode;
    }

    /// <summary>The HTTP status to return to the API caller for this failure.</summary>
    public int ToHttpStatus() => Kind switch
    {
        // Pass a provider 4xx the caller can act on straight through; anything else is a 400 fallback.
        UpvestFailureKind.InvalidRequest =>
            ProviderStatusCode is >= 400 and < 500 ? ProviderStatusCode.Value : 400,
        // Our credentials/quota/transport — the caller cannot fix it.
        _ => 502
    };
}
