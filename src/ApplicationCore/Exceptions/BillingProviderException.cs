using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public enum BillingFailureKind
{
    /// <summary>The billing system did not answer within the request budget.</summary>
    Timeout,
    /// <summary>The billing system could not be reached (connection failure).</summary>
    Unreachable,
    /// <summary>The billing system rejected our credentials or configuration (401/403, unknown product family).</summary>
    Misconfigured,
    /// <summary>The billing system rejected the request's content (400/422).</summary>
    Rejected,
    /// <summary>The billing system throttled us (429).</summary>
    RateLimited,
    /// <summary>The billing system failed or answered with something we could not read.</summary>
    ProviderError
}

/// <summary>
/// A billing-system failure translated at the integration boundary. <see cref="Exception.Message"/> is
/// caller-safe: it never carries URLs, credentials, SDK type names or request bodies.
/// </summary>
public class BillingProviderException : Exception
{
    public BillingProviderException(BillingFailureKind kind, string message, int? providerStatusCode = null,
        IReadOnlyList<string>? providerMessages = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        ProviderStatusCode = providerStatusCode;
        ProviderMessages = providerMessages ?? Array.Empty<string>();
    }

    public BillingFailureKind Kind { get; }

    /// <summary>The HTTP status the billing system answered with, when it answered at all.</summary>
    public int? ProviderStatusCode { get; }

    /// <summary>Validation messages returned by the billing system (422), safe to show to the caller.</summary>
    public IReadOnlyList<string> ProviderMessages { get; }

    /// <summary>
    /// True when a write may have taken effect even though we did not get a usable answer: no answer at all, a 5xx,
    /// or a response we could not read. Rejections (4xx) are definitive.
    /// </summary>
    public bool IsOutcomeUncertain => Kind is BillingFailureKind.Timeout or BillingFailureKind.Unreachable
        or BillingFailureKind.ProviderError;
}

/// <summary>
/// A write was sent to the billing system but neither its response nor a follow-up lookup could confirm whether it
/// took effect. The local claim stays pending and is settled by the next request that touches it.
/// </summary>
public class BillingOutcomeUnknownException : BillingProviderException
{
    public BillingOutcomeUnknownException(string message, BillingProviderException cause)
        : base(cause.Kind, message, cause.ProviderStatusCode, cause.ProviderMessages, cause)
    {
    }
}

/// <summary>Another request for the same buyer is already creating billing records.</summary>
public class BillingOperationInProgressException : Exception
{
    public BillingOperationInProgressException(string message) : base(message)
    {
    }
}
