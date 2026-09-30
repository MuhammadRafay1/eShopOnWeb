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

    public void AttachAuthorization(string payPalOrderId, string authorizationId, string authorizationStatus,
        string currency, string? cardBrand, string? cardLast4)
    {
        Payment = new Payment(payPalOrderId, authorizationId, authorizationStatus, currency, cardBrand, cardLast4);
        Status = OrderStatus.Authorized;
    }

    public void RenewAuthorization(string newAuthorizationId, string status)
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment!.RenewAuthorization(newAuthorizationId, status);
    }

    public void RecordCapture(string captureId, string captureStatus, decimal capturedAmount,
        decimal payPalFee, decimal netAmount, DateTimeOffset capturedAt)
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment!.RecordCapture(captureId, captureStatus, capturedAmount, payPalFee, netAmount, capturedAt);
        Status = OrderStatus.Fulfilled;
    }

    public void Cancel()
    {
        Status = OrderStatus.Cancelled;
    }

    public void RecordRefund(PaymentRefund refund)
    {
        Guard.Against.Null(Payment, nameof(Payment));
        Payment!.AddRefund(refund);
        Status = Payment.TotalRefunded() >= Payment.CapturedAmount
            ? OrderStatus.Refunded
            : OrderStatus.PartiallyRefunded;
    }
}
