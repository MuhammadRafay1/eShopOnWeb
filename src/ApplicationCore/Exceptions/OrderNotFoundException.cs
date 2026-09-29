using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when an order does not exist OR belongs to a different shopper. The same exception is
/// used for both cases deliberately, so the API layer maps both to 404 without leaking which one
/// it was (a shopper must never learn that another shopper's order exists).
/// </summary>
public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int orderId) : base($"No order found with id {orderId}")
    {
    }
}
