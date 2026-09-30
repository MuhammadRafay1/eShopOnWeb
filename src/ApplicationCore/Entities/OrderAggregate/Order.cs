using System;
using System.Collections.Generic;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
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

    /// <summary>Fulfilment lifecycle. Payment/refund detail lives on <see cref="Payment"/>.</summary>
    public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;

    /// <summary>The single payment for this order, populated once the shopper pays.</summary>
    public Payment? Payment { get; private set; }

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

    /// <summary>
    /// Attaches the authorized <see cref="Payment"/> and moves the order to
    /// <see cref="OrderStatus.PaymentAuthorized"/>. Legal only from
    /// <see cref="OrderStatus.AwaitingPayment"/>.
    /// </summary>
    public void MarkPaymentAuthorized(Payment payment)
    {
        Guard.Against.Null(payment, nameof(payment));
        if (Status != OrderStatus.AwaitingPayment)
            throw new OrderStateException(
                $"Order {Id} cannot be authorized from state {Status}.");

        Payment = payment;
        Status = OrderStatus.PaymentAuthorized;
    }

    /// <summary>Moves to <see cref="OrderStatus.Fulfilled"/>. Legal only once authorized.</summary>
    public void MarkFulfilled()
    {
        if (Status != OrderStatus.PaymentAuthorized)
            throw new OrderStateException(
                $"Order {Id} cannot be fulfilled from state {Status}; it must be PaymentAuthorized.");

        Status = OrderStatus.Fulfilled;
    }

    /// <summary>
    /// Moves to <see cref="OrderStatus.Cancelled"/>. Legal only before fulfilment
    /// (AwaitingPayment or PaymentAuthorized).
    /// </summary>
    public void MarkCancelled()
    {
        if (Status != OrderStatus.AwaitingPayment && Status != OrderStatus.PaymentAuthorized)
            throw new OrderStateException(
                $"Order {Id} cannot be cancelled from state {Status}.");

        Status = OrderStatus.Cancelled;
    }
}
