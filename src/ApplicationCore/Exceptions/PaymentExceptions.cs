using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>The request is malformed or names something it may not use (maps to 400).</summary>
public class PaymentValidationException : Exception
{
    public PaymentValidationException(string message) : base(message) { }
}

/// <summary>The order/payment is not in a state that allows the action (maps to 409).</summary>
public class PaymentStateException : Exception
{
    public PaymentStateException(string message) : base(message) { }
}

/// <summary>Another request is acting on the same payment right now, or won a race for it (maps to 409).</summary>
public class PaymentConflictException : Exception
{
    public PaymentConflictException(string message) : base(message) { }
}

/// <summary>The resource does not exist or does not belong to the caller (maps to 404).</summary>
public class PaymentResourceNotFoundException : Exception
{
    public PaymentResourceNotFoundException(string message) : base(message) { }
}

public enum PaymentProviderFailure
{
    /// <summary>The provider refused the request as invalid for this payment (4xx the caller can act on).</summary>
    Rejected,
    /// <summary>The card/issuer declined.</summary>
    Declined,
    /// <summary>The provider requires a shopper challenge (e.g. 3-D Secure) that this API cannot complete.</summary>
    PayerActionRequired,
    /// <summary>The provider did not answer in time or could not be reached; nothing is known to have happened.</summary>
    Unavailable,
    /// <summary>The provider may have acted but did not confirm; the outcome is settled on the next attempt.</summary>
    OutcomeUnknown,
    /// <summary>The provider rejected OUR credentials/permissions — an operator problem, not the caller's.</summary>
    Configuration,
    /// <summary>The provider answered with something that could not be understood.</summary>
    InvalidResponse
}

/// <summary>
/// A failure talking to the payment provider, already translated into the application's terms.
/// Messages are safe to show to callers: they never contain card data or SDK internals.
/// </summary>
public class PaymentProviderException : Exception
{
    public PaymentProviderException(PaymentProviderFailure failure, string message, int? providerStatusCode = null,
        string? providerErrorName = null, string? providerIssue = null, string? providerDebugId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
        ProviderStatusCode = providerStatusCode;
        ProviderErrorName = providerErrorName;
        ProviderIssue = providerIssue;
        ProviderDebugId = providerDebugId;
    }

    public PaymentProviderFailure Failure { get; }
    public int? ProviderStatusCode { get; }
    public string? ProviderErrorName { get; }
    public string? ProviderIssue { get; }
    /// <summary>PayPal's correlation id for the failed call — quote it to PayPal support.</summary>
    public string? ProviderDebugId { get; }

    /// <summary>True when the provider definitely did not act (safe to release a claim and let the caller retry).</summary>
    public bool ProviderDidNotAct => Failure is PaymentProviderFailure.Rejected or PaymentProviderFailure.Declined
        or PaymentProviderFailure.PayerActionRequired or PaymentProviderFailure.Configuration;
}
