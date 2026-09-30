using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown at fulfilment when a stale PayPal authorization can no longer be renewed (e.g. past the
/// 30-day reauthorization window, or PayPal declined the reauthorization). Carries PayPal's own
/// explanation so an operator can decide what to do next (typically: collect payment again).
/// </summary>
public class AuthorizationNotRenewableException : Exception
{
    public string? DebugId { get; }

    public AuthorizationNotRenewableException(string message, string? debugId = null) : base(message)
    {
        DebugId = debugId;
    }
}
