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

    // --- Payment state (additive; the original checkout flow leaves these at their defaults) ---

    /// <summary>The application's own order lifecycle state. Defaults to awaiting payment.</summary>
    public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;

    private OrderPayment? _payment;

    /// <summary>The 1:1 payment record carrying the state PayPal owns. Null for legacy/storefront orders.</summary>
    public OrderPayment? Payment => _payment;

    /// <summary>
    /// Attaches a fresh payment record so this order can be paid through the API. Called once, when
    /// an order is placed via <c>POST /api/orders</c>.
    /// </summary>
    public void InitializePayment(string currency, string invoiceReference)
    {
        if (_payment is not null)
            throw new InvalidOrderStateException("Order already has a payment.");
        _payment = new OrderPayment(currency, invoiceReference);
    }

    private OrderPayment RequirePayment() =>
        _payment ?? throw new InvalidOrderStateException("Order has no payment to act on.");

    private void Require(OrderStatus expected, string action)
    {
        if (Status != expected)
            throw new InvalidOrderStateException($"Cannot {action} an order in state {Status}; expected {expected}.");
    }

    /// <summary>Claim the order for authorization: AwaitingPayment → Authorizing.</summary>
    public void BeginAuthorizing()
    {
        Require(OrderStatus.AwaitingPayment, "pay for");
        Status = OrderStatus.Authorizing;
        RequirePayment().Touch();
    }

    /// <summary>Record a successful hold: Authorizing → Authorized.</summary>
    public void MarkAuthorized(string payPalOrderId, string authorizationId, string? authorizationStatus, decimal? authorizedAmount, DateTimeOffset? expiresAt)
    {
        Require(OrderStatus.Authorizing, "mark authorized");
        var p = RequirePayment();
        p.ApplyAuthorization(payPalOrderId, authorizationId, authorizationStatus, authorizedAmount, expiresAt);
        p.Touch();
        Status = OrderStatus.Authorized;
    }

    /// <summary>Authorization did not place a hold (declined/rejected): revert to AwaitingPayment so the shopper can retry.</summary>
    public void FailAuthorization()
    {
        Require(OrderStatus.Authorizing, "fail authorization for");
        RequirePayment().Touch();
        Status = OrderStatus.AwaitingPayment;
    }

    /// <summary>Claim the order for capture: Authorized → Capturing.</summary>
    public void BeginCapturing()
    {
        Require(OrderStatus.Authorized, "fulfil");
        Status = OrderStatus.Capturing;
        RequirePayment().Touch();
    }

    /// <summary>Update the authorization after a reauthorization renewed a stale hold.</summary>
    public void ApplyReauthorization(string authorizationId, string? authorizationStatus, decimal? authorizedAmount, DateTimeOffset? expiresAt)
    {
        var p = RequirePayment();
        p.ApplyReauthorization(authorizationId, authorizationStatus, authorizedAmount, expiresAt);
        p.Touch();
    }

    /// <summary>Record the capture and fulfil: Capturing → Fulfilled.</summary>
    public void MarkFulfilled(string captureId, string? captureStatus, decimal capturedAmount, decimal? fee, decimal? net)
    {
        Require(OrderStatus.Capturing, "mark fulfilled");
        var p = RequirePayment();
        p.ApplyCapture(captureId, captureStatus, capturedAmount, fee, net);
        p.Touch();
        Status = OrderStatus.Fulfilled;
    }

    /// <summary>Capture failed; revert to Authorized so the operator can retry fulfilment.</summary>
    public void FailCapture()
    {
        Require(OrderStatus.Capturing, "fail capture for");
        RequirePayment().Touch();
        Status = OrderStatus.Authorized;
    }

    /// <summary>Claim the order for cancellation: AwaitingPayment or Authorized → Cancelling.</summary>
    public void BeginCancelling()
    {
        if (Status != OrderStatus.AwaitingPayment && Status != OrderStatus.Authorized)
            throw new InvalidOrderStateException($"Cannot cancel an order in state {Status}; expected AwaitingPayment or Authorized.");
        PreCancelStatus = Status;
        Status = OrderStatus.Cancelling;
        RequirePayment().Touch();
    }

    /// <summary>The status held before cancellation began, so a failed void can revert accurately.</summary>
    public OrderStatus PreCancelStatus { get; private set; } = OrderStatus.AwaitingPayment;

    /// <summary>Record the void (or no-op cancel of an unpaid order): Cancelling → Cancelled.</summary>
    public void MarkCancelled(string? authorizationStatus = null)
    {
        Require(OrderStatus.Cancelling, "mark cancelled");
        var p = RequirePayment();
        if (authorizationStatus is not null) p.ApplyVoid(authorizationStatus);
        p.Touch();
        Status = OrderStatus.Cancelled;
    }

    /// <summary>Void failed; revert to the status held before the cancel claim.</summary>
    public void FailCancellation()
    {
        Require(OrderStatus.Cancelling, "fail cancellation for");
        RequirePayment().Touch();
        Status = PreCancelStatus;
    }

    /// <summary>
    /// Record a refund against the capture. <paramref name="fullyRefunded"/> decides whether the order
    /// becomes <see cref="OrderStatus.Refunded"/> or <see cref="OrderStatus.PartiallyRefunded"/>.
    /// </summary>
    public void MarkRefunded(bool fullyRefunded)
    {
        if (Status != OrderStatus.Fulfilled && Status != OrderStatus.PartiallyRefunded)
            throw new InvalidOrderStateException($"Cannot refund an order in state {Status}; expected Fulfilled or PartiallyRefunded.");
        RequirePayment().Touch();
        Status = fullyRefunded ? OrderStatus.Refunded : OrderStatus.PartiallyRefunded;
    }

    /// <summary>
    /// Record a refund attempt against the capture before calling PayPal (the duplicate-claim row).
    /// </summary>
    public void AttachRefund(OrderRefund refund)
    {
        var p = RequirePayment();
        p.AddRefund(refund);
        p.Touch();
    }

    /// <summary>A PayPal write could not be confirmed; park the order for a human/sweep to reconcile.</summary>
    public void MarkUnknown()
    {
        _payment?.Touch();
        Status = OrderStatus.Unknown;
    }
}
