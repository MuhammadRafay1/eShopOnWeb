using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>Thrown when an order is placed with no items. Maps to 422.</summary>
public class OrderItemsRequiredException : Exception
{
    public OrderItemsRequiredException()
        : base("An order must contain at least one item.")
    {
    }
}
