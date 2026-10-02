using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A single refund issued against an order's captured payment. One capture can have several
/// partial refunds; each carries the caller-supplied idempotency key that guards against
/// refunding twice for the same request.
/// </summary>
public class PaymentRefund : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentRefund() { }

    public PaymentRefund(string idempotencyKey, decimal amount)
    {
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));
        Guard.Against.NegativeOrZero(amount, nameof(amount));

        IdempotencyKey = idempotencyKey;
        Amount = amount;
        Status = "PENDING";
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The caller-supplied idempotency key. Repeating a request under the same key must not refund twice.</summary>
    public string IdempotencyKey { get; private set; }

    /// <summary>The amount refunded by this request.</summary>
    public decimal Amount { get; private set; }

    /// <summary>PayPal's id for this refund (null until PayPal has confirmed it).</summary>
    public string? PayPalRefundId { get; private set; }

    /// <summary>PayPal's current status for this refund (PENDING/COMPLETED/CANCELLED/FAILED).</summary>
    public string Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Records PayPal's response once the refund call has returned.</summary>
    public void Settle(string payPalRefundId, string status)
    {
        Guard.Against.NullOrEmpty(payPalRefundId, nameof(payPalRefundId));
        PayPalRefundId = payPalRefundId;
        Status = string.IsNullOrEmpty(status) ? "COMPLETED" : status;
    }

    /// <summary>Marks the refund as of unknown outcome (connection failed after the call may have landed).</summary>
    public void MarkUnknown() => Status = "UNKNOWN";

    public void MarkFailed() => Status = "FAILED";

    /// <summary>A refund counts against the captured total unless it was cancelled or failed outright.</summary>
    public bool CountsTowardRefundedTotal =>
        !string.Equals(Status, "FAILED", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);
}
