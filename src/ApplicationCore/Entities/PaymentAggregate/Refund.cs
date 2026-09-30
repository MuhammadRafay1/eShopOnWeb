using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A single refund against a <see cref="Payment"/>'s capture. The caller-supplied
/// <see cref="IdempotencyKey"/> is unique per payment (enforced by a DB constraint), so
/// replaying a refund request under the same key returns the existing row rather than
/// refunding twice, while two distinct partial refunds remain legitimate.
/// </summary>
public class Refund : BaseEntity
{
    public int PaymentId { get; private set; }
    public string PayPalRefundId { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>PayPal's own refund status, stored verbatim: COMPLETED / PENDING / CANCELLED / FAILED.</summary>
    public string Status { get; private set; }

    public string IdempotencyKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

#pragma warning disable CS8618 // Required by Entity Framework
    private Refund() { }
#pragma warning restore CS8618

    public Refund(int paymentId, string payPalRefundId, decimal amount, string status, string idempotencyKey)
    {
        Guard.Against.NullOrEmpty(payPalRefundId, nameof(payPalRefundId));
        Guard.Against.Negative(amount, nameof(amount));
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        PaymentId = paymentId;
        PayPalRefundId = payPalRefundId;
        Amount = amount;
        Status = status;
        IdempotencyKey = idempotencyKey;
    }
}
