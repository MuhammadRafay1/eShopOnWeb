using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

/// <summary>
/// Thrown when an operation is attempted on an <see cref="Entities.OrderAggregate.Order"/>
/// that its current state does not permit (e.g. fulfilling an order that was never paid).
/// </summary>
public class OrderStateException : Exception
{
    public OrderStateException(string message) : base(message)
    {
    }
}
