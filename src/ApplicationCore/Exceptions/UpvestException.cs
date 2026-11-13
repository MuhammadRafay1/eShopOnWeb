using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// The single failure type the Upvest integration boundary raises for every provider, transport or
/// configuration failure. Carries enough to map the failure to a caller-facing status without
/// leaking the SDK's exception types or any provider detail that is not caller-safe.
/// </summary>
public class UpvestException : Exception
{
    public UpvestException(
        string message,
        int? providerStatusCode = null,
        bool callerFault = false,
        bool unknownOutcome = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderStatusCode = providerStatusCode;
        CallerFault = callerFault;
        UnknownOutcome = unknownOutcome;
    }

    /// <summary>The HTTP status Upvest returned, when the provider answered; null for a transport failure.</summary>
    public int? ProviderStatusCode { get; }

    /// <summary>True when the provider rejected the caller's own request (a 4xx they can act on).</summary>
    public bool CallerFault { get; }

    /// <summary>True when a write's outcome could not be settled — it may have landed at Upvest.</summary>
    public bool UnknownOutcome { get; }
}
