using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// A failure surfaced by the subscription-billing integration.
/// <see cref="CallerFault"/> distinguishes a request the caller can fix (bad plan handle,
/// provider-side validation) from an integration/availability problem.
/// <see cref="UnknownOutcome"/> marks a write whose transport failed after the bytes may
/// have reached the provider — the outcome must be settled by re-reading, never reported as a plain failure.
/// </summary>
public class SubscriptionBillingException : Exception
{
    public int? HttpStatusCode { get; }
    public bool CallerFault { get; }
    public bool UnknownOutcome { get; }

    public SubscriptionBillingException(string message,
        int? httpStatusCode = null,
        bool callerFault = false,
        bool unknownOutcome = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        HttpStatusCode = httpStatusCode;
        CallerFault = callerFault;
        UnknownOutcome = unknownOutcome;
    }
}