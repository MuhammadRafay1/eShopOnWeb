using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown both when an order truly does not exist and when it exists but belongs to a different buyer -
/// callers must not be able to distinguish "not yours" from "not found". Maps to HTTP 404.
/// </summary>
public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int orderId) : base($"No order found with id {orderId}")
    {
    }
}
