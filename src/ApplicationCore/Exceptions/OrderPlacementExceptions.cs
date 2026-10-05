using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Thrown when an order is placed with no items.</summary>
public class OrderItemsRequiredException : Exception
{
    public OrderItemsRequiredException() : base("An order must contain at least one item.") { }
}

/// <summary>Thrown when an order line is invalid (unknown catalog item or non-positive quantity).</summary>
public class InvalidOrderLineException : Exception
{
    public InvalidOrderLineException(string message) : base(message) { }
}
