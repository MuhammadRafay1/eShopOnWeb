using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when an order does not exist, or exists but is not owned by the caller. The same
/// exception is used for both cases deliberately so the API returns 404 without leaking the
/// existence of another shopper's order. Maps to HTTP 404 Not Found at the API edge.
/// </summary>
public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int orderId) : base($"No order found with id {orderId}")
    {
    }
}
