using System;
using System.Collections.Generic;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

public class Order : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Order() {}

    public Order(string buyerId, Address shipToAddress, List<OrderItem> items)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        BuyerId = buyerId;
        ShipToAddress = shipToAddress;
        _orderItems = items;
    }

    public string BuyerId { get; private set; }
    public DateTimeOffset OrderDate { get; private set; } = DateTimeOffset.Now;
    public Address ShipToAddress { get; private set; }

    /// <summary>Payment lifecycle state. Defaults to AwaitingPayment when an order is placed.</summary>
    public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;

    /// <summary>PayPal-owned payment state; null until /pay is first called.</summary>
    public OrderPayment? Payment { get; private set; }

    /// <summary>
    /// Attaches the payment record when /pay first authorizes the order and moves it to Authorized.
    /// Idempotent: calling again while already past AwaitingPayment is a no-op.
    /// </summary>
    public void BeginPayment(OrderPayment payment)
    {
        Guard.Against.Null(payment, nameof(payment));
        if (Status != OrderStatus.AwaitingPayment)
        {
            return;
        }
        Payment = payment;
        Status = OrderStatus.Authorized;
    }

    public void MarkFulfilled()
    {
        if (Status != OrderStatus.Authorized)
        {
            throw new InvalidOperationException(
                $"Order {Id} cannot be fulfilled from status {Status}; it must be Authorized.");
        }
        Status = OrderStatus.Fulfilled;
    }

    public void MarkCancelled()
    {
        if (Status is not (OrderStatus.AwaitingPayment or OrderStatus.Authorized))
        {
            throw new InvalidOperationException(
                $"Order {Id} cannot be cancelled from status {Status}; cancellation is only allowed before fulfilment.");
        }
        Status = OrderStatus.Cancelled;
    }

    /// <summary>
    /// Moves a fulfilled order to PartiallyRefunded or Refunded. Only valid after fulfilment.
    /// </summary>
    public void ApplyRefund(bool isFullRefund)
    {
        if (Status is not (OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded))
        {
            throw new InvalidOperationException(
                $"Order {Id} cannot be refunded from status {Status}; it must be Fulfilled first.");
        }
        Status = isFullRefund ? OrderStatus.Refunded : OrderStatus.PartiallyRefunded;
    }

    // DDD Patterns comment
    // Using a private collection field, better for DDD Aggregate's encapsulation
    // so OrderItems cannot be added from "outside the AggregateRoot" directly to the collection,
    // but only through the method Order.AddOrderItem() which includes behavior.
    private readonly List<OrderItem> _orderItems = new List<OrderItem>();

    // Using List<>.AsReadOnly() 
    // This will create a read only wrapper around the private list so is protected against "external updates".
    // It's much cheaper than .ToList() because it will not have to copy all items in a new collection. (Just one heap alloc for the wrapper instance)
    //https://msdn.microsoft.com/en-us/library/e78dcd75(v=vs.110).aspx 
    public IReadOnlyCollection<OrderItem> OrderItems => _orderItems.AsReadOnly();

    public decimal Total()
    {
        var total = 0m;
        foreach (var item in _orderItems)
        {
            total += item.UnitPrice * item.Units;
        }
        return total;
    }
}
