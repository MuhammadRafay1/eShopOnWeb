using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Raised when an order cannot be placed because the request is invalid (bad item or quantity).</summary>
public class OrderPlacementException : Exception
{
    public OrderPlacementException(string message) : base(message)
    {
    }
}
