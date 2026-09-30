using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class InvalidOrderStateException : Exception
{
    public InvalidOrderStateException(int orderId, OrderStatus currentStatus, OrderStatus attemptedStatus)
        : base($"Order {orderId} cannot move from {currentStatus} to {attemptedStatus}.")
    {
        OrderId = orderId;
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }

    public int OrderId { get; }
    public OrderStatus CurrentStatus { get; }
    public OrderStatus AttemptedStatus { get; }
}
