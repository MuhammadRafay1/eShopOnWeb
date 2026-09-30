using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when an action is requested against an order that is in the wrong lifecycle stage —
/// e.g. paying an order that is not awaiting payment, fulfilling before payment, cancelling after
/// fulfilment, or refunding an order that was never captured.
/// </summary>
public class InvalidOrderStateException : Exception
{
    public InvalidOrderStateException(string message) : base(message)
    {
    }
}
