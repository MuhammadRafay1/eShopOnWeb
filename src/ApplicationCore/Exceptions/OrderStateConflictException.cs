using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when a payment lifecycle action is attempted against an order in the wrong state
/// (e.g. cancelling an already-fulfilled order). Maps to HTTP 409.
/// </summary>
public class OrderStateConflictException : Exception
{
    public OrderStateConflictException(string message) : base(message)
    {
    }
}
