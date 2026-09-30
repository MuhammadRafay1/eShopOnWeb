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
    public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;
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
    /// Records a successful authorization (hold) of the order total. Idempotent: calling this again
    /// while already <see cref="OrderStatus.Authorized"/> or beyond is a no-op for the status, but the
    /// caller should not normally call it again once authorized (see <see cref="IsAuthorized"/>).
    /// </summary>
    public void MarkAuthorized(string currency, string payPalOrderId, string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        if (Status != OrderStatus.AwaitingPayment)
        {
            throw new InvalidOperationException($"Order {Id} cannot be authorized from status {Status}.");
        }

        Payment ??= new Payment(currency);
        Payment.MarkAuthorized(payPalOrderId, authorizationId, status, expiresAt);
        Status = OrderStatus.Authorized;
    }

    public bool IsAuthorized => Status == OrderStatus.Authorized && Payment?.AuthorizationId is not null;

    public void ReplaceAuthorization(string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        if (Payment is null)
        {
            throw new InvalidOperationException($"Order {Id} has no payment to reauthorize.");
        }

        Payment.ReplaceAuthorization(authorizationId, status, expiresAt);
    }

    public void MarkFulfilled(string captureId, decimal capturedAmount, decimal payPalFee, decimal netAmount)
    {
        if (Status != OrderStatus.Authorized)
        {
            throw new InvalidOperationException($"Order {Id} cannot be fulfilled from status {Status}.");
        }

        Payment!.MarkCaptured(captureId, "COMPLETED", capturedAmount, payPalFee, netAmount);
        Status = OrderStatus.Fulfilled;
    }

    public void MarkCancelled()
    {
        if (Status == OrderStatus.Cancelled)
        {
            return; // idempotent
        }

        if (Status != OrderStatus.Authorized)
        {
            throw new InvalidOperationException($"Order {Id} cannot be cancelled from status {Status}. Use a refund once fulfilled.");
        }

        Payment!.MarkVoided();
        Status = OrderStatus.Cancelled;
    }

    public Refund AddRefund(string payPalRefundId, decimal amount, string status, string idempotencyKey)
    {
        if (Payment?.CaptureId is null)
        {
            throw new InvalidOperationException($"Order {Id} has not been fulfilled; nothing to refund.");
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Refund amount must be positive.");
        }

        if (Payment.TotalRefunded() + amount > Payment.CapturedAmount!.Value)
        {
            throw new InvalidOperationException(
                $"Order {Id} refund of {amount} would exceed the captured amount of {Payment.CapturedAmount.Value} (already refunded {Payment.TotalRefunded()}).");
        }

        var refund = new Refund(payPalRefundId, amount, status, idempotencyKey);
        Payment.AddRefund(refund);

        Status = Payment.RefundableRemaining() <= 0m ? OrderStatus.Refunded : OrderStatus.PartiallyRefunded;

        return refund;
    }

    public decimal TotalRefunded() => Payment?.TotalRefunded() ?? 0m;

    public decimal RefundableRemaining() => Payment?.RefundableRemaining() ?? 0m;

    public Refund? FindRefundByKey(string idempotencyKey) => Payment?.FindRefundByKey(idempotencyKey);
}
