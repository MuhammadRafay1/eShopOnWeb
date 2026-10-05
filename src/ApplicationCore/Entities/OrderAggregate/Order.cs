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

    public OrderStatus Status { get; private set; } = OrderStatus.AwaitingPayment;

    public OrderPayment? Payment { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token: every payment state change rotates it, so two requests racing on the
    /// same order cannot both commit (the second save is rejected by the store).
    /// </summary>
    public Guid ConcurrencyStamp { get; private set; } = Guid.NewGuid();

    public decimal Total()
    {
        var total = 0m;
        foreach (var item in _orderItems)
        {
            total += item.UnitPrice * item.Units;
        }
        return total;
    }

    /// <summary>Starts (or restarts, after a decline) a payment attempt for the order total.</summary>
    public OrderPayment BeginPayment(string provider, string currency, int? savedPaymentMethodId)
    {
        if (Status != OrderStatus.AwaitingPayment)
            throw new PaymentStateException($"Order {Id} cannot be paid because it is {Status}.");

        if (Payment is null)
            Payment = new OrderPayment(provider, currency, Total());
        Payment.StartAttempt(currency, Total(), savedPaymentMethodId);
        Touch();
        return Payment;
    }

    public void RecordProviderOrder(string providerOrderId, string? providerOrderStatus)
    {
        RequirePayment().RecordProviderOrder(providerOrderId, providerOrderStatus);
        Touch();
    }

    public void RecordAuthorization(string authorizationId, string? authorizationStatus, decimal? amount,
        DateTimeOffset? authorizedAt, DateTimeOffset? expiresAt, string? cardBrand, string? cardLastDigits)
    {
        var payment = RequirePayment();
        payment.RecordAuthorization(authorizationId, authorizationStatus, amount, authorizedAt, expiresAt);
        payment.RecordCard(cardBrand, cardLastDigits);
        Status = OrderStatus.PaymentAuthorized;
        Touch();
    }

    /// <summary>The attempt did not produce a usable hold; the order can be paid again.</summary>
    public void RecordPaymentFailure(PaymentStatus status, string reason, string? cardBrand = null, string? cardLastDigits = null)
    {
        var payment = RequirePayment();
        payment.RecordFailure(status, reason);
        payment.RecordCard(cardBrand, cardLastDigits);
        Status = OrderStatus.AwaitingPayment;
        Touch();
    }

    public void RecordReauthorization(string authorizationId, string? authorizationStatus, decimal? amount,
        DateTimeOffset? authorizedAt, DateTimeOffset? expiresAt)
    {
        EnsureStatus(OrderStatus.PaymentAuthorized, "renew its authorization");
        RequirePayment().RecordReauthorization(authorizationId, authorizationStatus, amount, authorizedAt, expiresAt);
        Touch();
    }

    public void RecordAuthorizationStatus(string? authorizationStatus)
    {
        RequirePayment().RecordAuthorizationStatus(authorizationStatus);
        Touch();
    }

    public void RecordPaymentNote(string note)
    {
        RequirePayment().RecordNote(note);
        Touch();
    }

    public void RecordCapture(string captureId, string? captureStatus, decimal? amount, decimal? fee, decimal? net,
        DateTimeOffset? capturedAt)
    {
        EnsureStatus(OrderStatus.PaymentAuthorized, "be fulfilled");
        RequirePayment().RecordCapture(captureId, captureStatus, amount, fee, net, capturedAt);
        Status = OrderStatus.Fulfilled;
        Touch();
    }

    /// <summary>Cancels before fulfilment. A held authorization must already have been voided by the caller.</summary>
    public void Cancel(string? voidedAuthorizationStatus, DateTimeOffset at)
    {
        if (Status is not (OrderStatus.AwaitingPayment or OrderStatus.PaymentAuthorized))
            throw new PaymentStateException($"Order {Id} cannot be cancelled because it is {Status}; refund it instead.");

        if (Payment?.AuthorizationId is not null && Status == OrderStatus.PaymentAuthorized)
            Payment.RecordVoid(voidedAuthorizationStatus, at);
        Status = OrderStatus.Cancelled;
        Touch();
    }

    /// <summary>
    /// Reserves a refund amount against the captured payment. The reservation counts against what remains
    /// refundable immediately, so concurrent refunds can never add up to more than was captured.
    /// </summary>
    public PaymentRefund ReserveRefund(string idempotencyKey, decimal? amount, DateTimeOffset at)
    {
        if (Status is not (OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded))
            throw new PaymentStateException($"Order {Id} cannot be refunded because it is {Status}.");

        var payment = RequirePayment();
        var refundable = payment.RefundableAmount;
        var requested = amount ?? refundable;
        if (requested <= 0m)
            throw new PaymentStateException($"Order {Id} has nothing left to refund.");
        if (decimal.Round(requested, 2) != requested)
            throw new PaymentValidationException("Refund amount cannot have more than two decimal places.");
        if (requested > refundable)
            throw new PaymentStateException(
                $"Refund of {requested:0.00} {payment.Currency} exceeds the {refundable:0.00} {payment.Currency} still refundable on order {Id}.");

        var refund = payment.AddRefund(idempotencyKey, requested, at);
        Touch();
        return refund;
    }

    public void RecordRefundResult(PaymentRefund refund, string refundId, string? providerStatus, RefundState state,
        decimal? amount, DateTimeOffset? at)
    {
        refund.RecordProviderResult(refundId, providerStatus, state, amount, at);
        RecalculateRefundStatus();
    }

    public void RecordRefundFailure(PaymentRefund refund, string reason)
    {
        refund.MarkFailed(reason);
        RecalculateRefundStatus();
    }

    public void RecordRefundOutcomeUnknown(PaymentRefund refund, string reason)
    {
        refund.MarkOutcomeUnknown(reason);
        Touch();
    }

    private void RecalculateRefundStatus()
    {
        var payment = RequirePayment();
        payment.RecalculateRefundStatus();
        Status = payment.Status switch
        {
            PaymentStatus.Refunded => OrderStatus.Refunded,
            PaymentStatus.PartiallyRefunded => OrderStatus.PartiallyRefunded,
            _ => OrderStatus.Fulfilled
        };
        Touch();
    }

    private OrderPayment RequirePayment() =>
        Payment ?? throw new PaymentStateException($"Order {Id} has no payment.");

    private void EnsureStatus(OrderStatus expected, string action)
    {
        if (Status != expected)
            throw new PaymentStateException($"Order {Id} cannot {action} because it is {Status}.");
    }

    private void Touch() => ConcurrencyStamp = Guid.NewGuid();
}
