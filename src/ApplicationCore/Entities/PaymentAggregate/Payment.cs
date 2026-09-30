using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// The money-movement lifecycle for one <c>Order</c>, plus the PayPal-owned identifiers/statuses a
/// later request (fulfil, cancel, refund, reconciliation) needs to act on it. One Payment per Order,
/// linked by <see cref="OrderId"/> — kept as a separate aggregate so the existing Order/checkout flow
/// is untouched.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    public int OrderId { get; private set; }
    public string BuyerId { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Unique per payment; set as the PayPal order's invoice_id (primary reconciliation key).</summary>
    public string CorrelationReference { get; private set; }

    // PayPal hold state
    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    // PayPal capture state
    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }

    // Idempotency keys, persisted so a retry reuses the same PayPal-Request-Id.
    public string? PayRequestKey { get; private set; }
    public string? CaptureRequestKey { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }

    public Payment(int orderId, string buyerId, decimal amount, string currencyCode, string correlationReference)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));
        Guard.Against.NullOrEmpty(correlationReference, nameof(correlationReference));

        OrderId = orderId;
        BuyerId = buyerId;
        Amount = amount;
        CurrencyCode = currencyCode;
        CorrelationReference = correlationReference;
        Status = PaymentStatus.AwaitingPayment;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Persists the idempotency key for the authorize attempt, once, before any PayPal call.</summary>
    public void BeginAuthorization(string payRequestKey)
    {
        if (string.IsNullOrEmpty(PayRequestKey))
        {
            PayRequestKey = payRequestKey;
        }
    }

    public void SetPayPalOrder(string payPalOrderId)
    {
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        PayPalOrderId = payPalOrderId;
    }

    public void MarkAuthorized(string authorizationId, string status, decimal heldAmount, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));
        if (heldAmount != Amount)
        {
            throw new PaymentGatewayException(
                $"PayPal held {heldAmount} {CurrencyCode} but the order total is {Amount} {CurrencyCode}; refusing to accept a mismatched authorization.");
        }

        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
        Status = PaymentStatus.Authorized;
    }

    /// <summary>Applies a fresh status/expiry after a successful re-authorization of a stale hold.</summary>
    public void UpdateAuthorization(string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
    }

    /// <summary>Persists the idempotency key for the capture attempt, once, before any PayPal call.</summary>
    public void BeginCapture(string captureRequestKey)
    {
        if (string.IsNullOrEmpty(CaptureRequestKey))
        {
            CaptureRequestKey = captureRequestKey;
        }
    }

    public void MarkCaptured(string captureId, string status, decimal capturedAmount, decimal? fee, decimal? net)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        CaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = capturedAmount;
        PayPalFee = fee;
        NetAmount = net;
        Status = PaymentStatus.Fulfilled;
    }

    public void MarkVoided()
    {
        if (Status != PaymentStatus.AwaitingPayment && Status != PaymentStatus.Authorized)
        {
            throw new PaymentStateConflictException(
                $"Order {OrderId} cannot be canceled from state {Status}.");
        }

        Status = PaymentStatus.Canceled;
    }

    public decimal TotalRefunded() => _refunds.Sum(r => r.Amount);

    public Refund? FindRefundByKey(string idempotencyKey) =>
        _refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);

    /// <summary>
    /// Records a refund PayPal has already accepted. Guards that the cumulative refunded amount never
    /// exceeds what was captured, even though PayPal's own rejection is the authoritative backstop.
    /// </summary>
    public Refund AddRefund(string refundId, decimal amount, string status, string idempotencyKey)
    {
        if (CapturedAmount is null)
        {
            throw new PaymentStateConflictException($"Order {OrderId} has not been captured; nothing to refund.");
        }

        if (TotalRefunded() + amount > CapturedAmount.Value)
        {
            throw new PaymentRejectedException(
                $"Refund of {amount} {CurrencyCode} would exceed the captured amount of {CapturedAmount} {CurrencyCode} (already refunded {TotalRefunded()} {CurrencyCode}).");
        }

        var refund = new Refund(Id, refundId, amount, status, idempotencyKey);
        _refunds.Add(refund);

        Status = TotalRefunded() >= CapturedAmount.Value
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        return refund;
    }
}
