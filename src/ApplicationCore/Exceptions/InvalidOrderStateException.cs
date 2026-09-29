using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when an order lifecycle transition is attempted from a state that does not allow it
/// (e.g. fulfilling an order that was never paid). Maps to HTTP 409 Conflict at the API edge.
/// </summary>
public class InvalidOrderStateException : Exception
{
    public InvalidOrderStateException(string message) : base(message)
    {
    }
}
