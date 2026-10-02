using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// The money-movement and fulfilment state that follows a placed <see cref="OrderAggregate.Order"/>.
/// It is an additive aggregate: the existing Order/OrderItem model is untouched, and this holds the
/// PayPal ids and status for the hold (authorization), the capture, and any refunds — enough for a
/// later request to act on, not only the one that created it.
/// </summary>
public class OrderPayment : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private OrderPayment() { }

    public OrderPayment(int orderId, string buyerId, string currencyCode, decimal amount, string invoiceId)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(invoiceId, nameof(invoiceId));

        OrderId = orderId;
        BuyerId = buyerId;
        CurrencyCode = currencyCode;
        Amount = amount;
        InvoiceId = invoiceId;
        Status = PaymentStatus.AwaitingPayment;
        // A stable reference used to derive idempotency keys so a double-click never authorizes or
        // captures twice (the same key is replayed to PayPal, which dedupes server-side).
        PaymentReference = Guid.NewGuid().ToString("N");
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public int OrderId { get; private set; }
    public string BuyerId { get; private set; }
    public string CurrencyCode { get; private set; }

    /// <summary>The authoritative order total (from catalog prices) the hold must equal to the cent.</summary>
    public decimal Amount { get; private set; }

    /// <summary>A per-order unique external invoice id, used to line this order up against PayPal's own records.</summary>
    public string InvoiceId { get; private set; }

    public PaymentStatus Status { get; private set; }

    /// <summary>Stable value used to build PayPal-Request-Id idempotency keys for this order's writes.</summary>
    public string PaymentReference { get; private set; }

    // --- State PayPal owns, kept so a later request can act on it -------------------------------
    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<PaymentRefund> _refunds = new();
    public IReadOnlyCollection<PaymentRefund> Refunds => _refunds.AsReadOnly();

    // --- Derived idempotency keys --------------------------------------------------------------
    public string CreateOrderIdempotencyKey => PaymentReference;
    public string AuthorizeIdempotencyKey => PaymentReference + "-A";
    public string CaptureIdempotencyKey => PaymentReference + "-C";
    public string VoidIdempotencyKey => PaymentReference + "-V";

    // --- Transitions ---------------------------------------------------------------------------

    public void MarkAuthorized(string payPalOrderId, string authorizationId, string? authorizationStatus, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));

        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiresAt = expiresAt;
        Status = PaymentStatus.Authorized;
        FailureReason = null;
        Touch();
    }

    /// <summary>Records a renewed authorization (the id may change when PayPal reauthorizes).</summary>
    public void UpdateAuthorization(string authorizationId, string? authorizationStatus, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));
        AuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiresAt = expiresAt;
        Touch();
    }

    public void MarkCaptured(string captureId, string? captureStatus, decimal grossAmount, decimal? payPalFee, decimal? netAmount)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        CaptureId = captureId;
        CaptureStatus = captureStatus;
        CapturedAmount = grossAmount;
        PayPalFee = payPalFee;
        NetAmount = netAmount;
        Status = PaymentStatus.Captured;
        Touch();
    }

    public void MarkCancelled()
    {
        Status = PaymentStatus.Cancelled;
        AuthorizationStatus = "VOIDED";
        Touch();
    }

    public void MarkFailed(string? reason)
    {
        Status = PaymentStatus.Failed;
        FailureReason = reason;
        Touch();
    }

    /// <summary>
    /// Marks a definitive card decline and rotates the idempotency reference, so a later retry with a
    /// different card is a fresh PayPal order rather than a replay that PayPal would dedupe.
    /// </summary>
    public void MarkDeclined(string? reason)
    {
        Status = PaymentStatus.Failed;
        FailureReason = reason;
        PaymentReference = Guid.NewGuid().ToString("N");
        Touch();
    }

    /// <summary>Finds an existing refund attempt for the caller's idempotency key, if any.</summary>
    public PaymentRefund? FindRefundByKey(string idempotencyKey) =>
        _refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);

    public PaymentRefund AddRefundClaim(string idempotencyKey, decimal amount)
    {
        var refund = new PaymentRefund(idempotencyKey, amount);
        _refunds.Add(refund);
        Touch();
        return refund;
    }

    /// <summary>Re-derives the payment status from the captured amount and settled refunds.</summary>
    public void RecalculateRefundState()
    {
        if (CapturedAmount is null)
        {
            return;
        }

        var refunded = TotalRefunded();
        if (refunded <= 0m)
        {
            Status = PaymentStatus.Captured;
        }
        else if (refunded >= CapturedAmount.Value)
        {
            Status = PaymentStatus.Refunded;
        }
        else
        {
            Status = PaymentStatus.PartiallyRefunded;
        }
        Touch();
    }

    /// <summary>The sum of refunds that count against the captured total.</summary>
    public decimal TotalRefunded() =>
        _refunds.Where(r => r.CountsTowardRefundedTotal).Sum(r => r.Amount);

    /// <summary>How much of the captured payment can still be refunded.</summary>
    public decimal RefundableRemaining() => (CapturedAmount ?? 0m) - TotalRefunded();

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
