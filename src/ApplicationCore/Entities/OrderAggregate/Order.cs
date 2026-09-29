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

    // Payment / fulfilment state machine. Kept on the entity itself (rather than in the
    // endpoint or service layer) so the legal transitions can never be bypassed by a future
    // caller. Every transition guards its allowed source states.
    public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;

    public void MarkPaymentAuthorized()
    {
        Guard.Against.InvalidInput(Status, nameof(Status), s => s == OrderStatus.AwaitingPayment,
            "Order must be awaiting payment to authorize.");
        Status = OrderStatus.PaymentAuthorized;
    }

    public void MarkFulfilled()
    {
        Guard.Against.InvalidInput(Status, nameof(Status), s => s == OrderStatus.PaymentAuthorized,
            "Order must have an authorized payment to fulfil.");
        Status = OrderStatus.Fulfilled;
    }

    public void MarkCancelled()
    {
        Guard.Against.InvalidInput(Status, nameof(Status),
            s => s is OrderStatus.AwaitingPayment or OrderStatus.PaymentAuthorized,
            "Only an unfulfilled order can be cancelled.");
        Status = OrderStatus.Cancelled;
    }

    public void MarkPartiallyRefunded()
    {
        Guard.Against.InvalidInput(Status, nameof(Status),
            s => s is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded,
            "Only a fulfilled order can be refunded.");
        Status = OrderStatus.PartiallyRefunded;
    }

    public void MarkFullyRefunded()
    {
        Guard.Against.InvalidInput(Status, nameof(Status),
            s => s is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded,
            "Only a fulfilled order can be refunded.");
        Status = OrderStatus.Refunded;
    }
}
