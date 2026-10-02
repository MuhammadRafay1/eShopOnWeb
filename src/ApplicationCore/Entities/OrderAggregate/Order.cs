using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

    /// <summary>
    /// Also serves as an EF Core concurrency token: a status transition's UPDATE is conditioned on the
    /// status it was read with, so two concurrent requests acting on the same order never both succeed.
    /// </summary>
    [ConcurrencyCheck]
    public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;

    public OrderPayment? Payment { get; private set; }

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

    public OrderPayment BeginAuthorization(string currency, string paymentDescription)
    {
        if (Status != OrderStatus.AwaitingPayment && Status != OrderStatus.AuthorizationFailed)
        {
            throw new InvalidOperationException($"Order {Id} cannot be authorized from status {Status}.");
        }

        Status = OrderStatus.Authorizing;
        if (Payment is null)
        {
            Payment = new OrderPayment(Id, Total(), currency, paymentDescription);
        }
        else
        {
            Payment.ResetForNewAttempt(Total(), currency, paymentDescription);
        }

        return Payment;
    }

    public void RecordAuthorized(string payPalOrderId, string authorizationId, string authorizationStatus)
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment.RecordAuthorization(payPalOrderId, authorizationId, authorizationStatus);
        Status = OrderStatus.Authorized;
    }

    public void RecordAuthorizationFailed(string error)
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment.RecordAuthorizationFailure(error);
        Status = OrderStatus.AuthorizationFailed;
    }

    public void BeginCapture()
    {
        if (Status != OrderStatus.Authorized)
        {
            throw new InvalidOperationException($"Order {Id} cannot be fulfilled from status {Status}.");
        }

        Status = OrderStatus.Capturing;
    }

    public void RecordFulfilled(string captureId, string captureStatus, decimal capturedAmount, decimal? fee, decimal? net)
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment.RecordCapture(captureId, captureStatus, capturedAmount, fee, net);
        Status = OrderStatus.Fulfilled;
    }

    public void RecordFulfilmentFailed(string error)
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment.RecordFulfilmentFailure(error);
        // Land back on Authorized so fulfilment can be retried rather than dead-ending the order.
        Status = OrderStatus.Authorized;
    }

    public void BeginCancel()
    {
        if (Status != OrderStatus.Authorized)
        {
            throw new InvalidOperationException($"Order {Id} cannot be cancelled from status {Status}.");
        }

        Status = OrderStatus.Cancelling;
    }

    public void RecordCancelled()
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment.RecordVoid();
        Status = OrderStatus.Cancelled;
    }

    public void RecordCancelFailed(string error)
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment.RecordFulfilmentFailure(error);
        Status = OrderStatus.Authorized;
    }

    public void BeginRefund()
    {
        if (Status != OrderStatus.Fulfilled && Status != OrderStatus.PartiallyRefunded)
        {
            throw new InvalidOperationException($"Order {Id} cannot be refunded from status {Status}.");
        }

        Status = OrderStatus.Refunding;
    }

    public void RecordRefunded(Refund refund, string payPalRefundId, string status, decimal amount)
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment.RecordRefundResult(refund, payPalRefundId, status, amount);
        Status = Payment.RemainingRefundable() <= 0m ? OrderStatus.Refunded : OrderStatus.PartiallyRefunded;
    }

    public void RecordRefundFailed(string error, OrderStatus previousStatus)
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment.RecordFulfilmentFailure(error);
        Status = previousStatus;
    }
}
