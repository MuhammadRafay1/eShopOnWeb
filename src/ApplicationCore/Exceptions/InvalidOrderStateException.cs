using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when an order is asked to make a state transition its lifecycle does not allow
/// (e.g. fulfilling an order that was never paid). Maps to HTTP 409 at the API boundary.
/// </summary>
public class InvalidOrderStateException : Exception
{
    public InvalidOrderStateException(string message) : base(message)
    {
    }
}
