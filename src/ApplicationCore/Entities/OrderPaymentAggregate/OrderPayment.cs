using System;
using System.Collections.Generic;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderPaymentAggregate;

/// <summary>
/// 1:1 with an <see cref="Entities.OrderAggregate.Order"/>. Carries every piece of PayPal-owned state
/// (the hold, the capture, the refunds) so a later request can act on the same payment rather than
/// starting a new one. State machine: AwaitingPayment -> Authorized -> Captured -> (Partially)Refunded,
/// with Authorized -> Cancelled and AwaitingPayment -> Failed as the other terminal/side paths.
/// </summary>
public class OrderPayment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private OrderPayment() { }

    public OrderPayment(int orderId, string buyerId, string currencyCode)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));

        OrderId = orderId;
        BuyerId = buyerId;
        CurrencyCode = currencyCode;
        Status = OrderPaymentStatus.AwaitingPayment;
        TotalRefundedAmount = 0m;

        // Stable idempotency keys for the lifetime of this payment - generated once, reused on every
        // retry of the same logical operation so PayPal sees the same PayPal-Request-Id each time.
        AuthorizeRequestId = Guid.NewGuid().ToString("N");
        CaptureRequestId = Guid.NewGuid().ToString("N");
        VoidRequestId = Guid.NewGuid().ToString("N");
    }

    public int OrderId { get; private set; }
    public string BuyerId { get; private set; }
    public string CurrencyCode { get; private set; }
    public OrderPaymentStatus Status { get; private set; }

    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }
    public decimal AuthorizedAmount { get; private set; }
    public DateTimeOffset? AuthorizedAt { get; private set; }

    public string? CaptureId { get; private set; }
    public decimal? CapturedGrossAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }

    public decimal TotalRefundedAmount { get; private set; }

    public string AuthorizeRequestId { get; private set; }
    public string CaptureRequestId { get; private set; }
    public string VoidRequestId { get; private set; }

    private readonly List<OrderRefund> _refunds = new();
    public IReadOnlyCollection<OrderRefund> Refunds => _refunds.AsReadOnly();

    public void MarkAuthorized(string payPalOrderId, string authorizationId, DateTimeOffset? expiresAt, decimal authorizedAmount)
    {
        if (Status != OrderPaymentStatus.AwaitingPayment)
        {
            throw new OrderPaymentStateException(
                $"Cannot authorize order payment {Id}: expected status {OrderPaymentStatus.AwaitingPayment}, was {Status}.");
        }

        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));

        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationExpiresAt = expiresAt;
        AuthorizedAmount = authorizedAmount;
        AuthorizedAt = DateTimeOffset.UtcNow;
        Status = OrderPaymentStatus.Authorized;
    }

    public void MarkFailed()
    {
        if (Status != OrderPaymentStatus.AwaitingPayment)
        {
            throw new OrderPaymentStateException(
                $"Cannot fail order payment {Id}: expected status {OrderPaymentStatus.AwaitingPayment}, was {Status}.");
        }

        Status = OrderPaymentStatus.Failed;
    }

    public void RenewAuthorization(string authorizationId, DateTimeOffset? expiresAt)
    {
        if (Status != OrderPaymentStatus.Authorized)
        {
            throw new OrderPaymentStateException(
                $"Cannot renew authorization for order payment {Id}: expected status {OrderPaymentStatus.Authorized}, was {Status}.");
        }

        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));

        AuthorizationId = authorizationId;
        AuthorizationExpiresAt = expiresAt;
    }

    public void MarkCaptured(string captureId, decimal grossAmount, decimal fee, decimal netAmount)
    {
        if (Status != OrderPaymentStatus.Authorized)
        {
            throw new OrderPaymentStateException(
                $"Cannot capture order payment {Id}: expected status {OrderPaymentStatus.Authorized}, was {Status}.");
        }

        Guard.Against.NullOrEmpty(captureId, nameof(captureId));

        CaptureId = captureId;
        CapturedGrossAmount = grossAmount;
        PayPalFee = fee;
        NetAmount = netAmount;
        CapturedAt = DateTimeOffset.UtcNow;
        Status = OrderPaymentStatus.Captured;
    }

    public void MarkCancelled()
    {
        if (Status != OrderPaymentStatus.Authorized)
        {
            throw new OrderPaymentStateException(
                $"Cannot cancel order payment {Id}: expected status {OrderPaymentStatus.Authorized}, was {Status}.");
        }

        Status = OrderPaymentStatus.Cancelled;
    }

    public void AddRefund(string payPalRefundId, decimal amount, string idempotencyKey, string status)
    {
        if (Status != OrderPaymentStatus.Captured && Status != OrderPaymentStatus.PartiallyRefunded)
        {
            throw new OrderPaymentStateException(
                $"Cannot refund order payment {Id}: expected status {OrderPaymentStatus.Captured} or {OrderPaymentStatus.PartiallyRefunded}, was {Status}.");
        }

        if (CapturedGrossAmount is null)
        {
            throw new OrderPaymentStateException($"Cannot refund order payment {Id}: no captured amount on record.");
        }

        var newTotal = TotalRefundedAmount + amount;
        if (newTotal > CapturedGrossAmount.Value)
        {
            throw new RefundAmountExceededException(
                $"Refund of {amount} would bring total refunds for order payment {Id} to {newTotal}, exceeding the captured amount of {CapturedGrossAmount.Value}.");
        }

        _refunds.Add(new OrderRefund(payPalRefundId, amount, idempotencyKey, status));
        TotalRefundedAmount = newTotal;
        Status = TotalRefundedAmount == CapturedGrossAmount.Value
            ? OrderPaymentStatus.Refunded
            : OrderPaymentStatus.PartiallyRefunded;
    }
}
