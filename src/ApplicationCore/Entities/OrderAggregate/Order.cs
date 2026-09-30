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

    /// <summary>Current lifecycle state. Defaults to <see cref="OrderStatus.AwaitingPayment"/>.</summary>
    public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;

    /// <summary>
    /// ISO-4217 currency this order is priced in, snapshotted at creation from PayPal:Currency so a later
    /// configuration change never retroactively re-prices an existing order.
    /// </summary>
    public string Currency { get; private set; } = "USD";

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

    /// <summary>Records the currency this order is priced in (set once at creation).</summary>
    public void SetCurrency(string currency)
    {
        Guard.Against.NullOrEmpty(currency, nameof(currency));
        Currency = currency;
    }

    // --- Payment lifecycle state machine ---------------------------------------------------------
    // Legal transitions live here rather than in the endpoint handlers so the state machine cannot
    // be bypassed. Each method guards the current state before transitioning.

    public void MarkAuthorized()
    {
        RequireStatus(nameof(MarkAuthorized), OrderStatus.AwaitingPayment);
        Status = OrderStatus.Authorized;
    }

    public void MarkCancelled()
    {
        RequireStatus(nameof(MarkCancelled), OrderStatus.AwaitingPayment, OrderStatus.Authorized);
        Status = OrderStatus.Cancelled;
    }

    public void MarkFulfilled()
    {
        RequireStatus(nameof(MarkFulfilled), OrderStatus.Authorized);
        Status = OrderStatus.Fulfilled;
    }

    public void MarkPartiallyRefunded()
    {
        RequireStatus(nameof(MarkPartiallyRefunded), OrderStatus.Fulfilled, OrderStatus.PartiallyRefunded);
        Status = OrderStatus.PartiallyRefunded;
    }

    public void MarkRefunded()
    {
        RequireStatus(nameof(MarkRefunded), OrderStatus.Fulfilled, OrderStatus.PartiallyRefunded);
        Status = OrderStatus.Refunded;
    }

    /// <summary>
    /// Reverts an authorized order back to awaiting-payment. Used only when a payment hold has gone stale
    /// and can no longer be renewed, so the shopper can pay the same order again (creating a fresh hold).
    /// </summary>
    public void MarkAwaitingPayment()
    {
        RequireStatus(nameof(MarkAwaitingPayment), OrderStatus.Authorized);
        Status = OrderStatus.AwaitingPayment;
    }

    private void RequireStatus(string transition, params OrderStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
        {
            throw new OrderStateConflictException(
                $"Cannot {transition} order {Id}: it is {Status}.");
        }
    }
}
