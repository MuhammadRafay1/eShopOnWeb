using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Thrown for an unknown catalog item or a non-positive quantity. Maps to 422.</summary>
public class InvalidOrderItemException : Exception
{
    public InvalidOrderItemException(string message) : base(message)
    {
    }
}
