using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when PayPal responds to a card authorization with a challenge (e.g. 3DS / buyer
/// approval) that would require a browser round-trip. This integration is browserless by design,
/// so the request is surfaced as an actionable error instead of being silently completed.
/// </summary>
public class PaymentChallengeRequiredException : Exception
{
    public string? DebugId { get; }

    public PaymentChallengeRequiredException(string message, string? debugId = null) : base(message)
    {
        DebugId = debugId;
    }
}
