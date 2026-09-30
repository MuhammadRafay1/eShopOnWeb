using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A single refund against a <see cref="Payment"/>'s capture. Not an aggregate root — only reachable
/// through <see cref="Payment.Refunds"/>. The caller-supplied <see cref="IdempotencyKey"/> is unique
/// per payment, which is what makes a replayed refund request a no-op.
/// </summary>
public class PaymentRefund : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentRefund() { }
    #pragma warning restore CS8618

    public PaymentRefund(string payPalRefundId, decimal amount, string status, string idempotencyKey)
    {
        Guard.Against.NullOrEmpty(payPalRefundId, nameof(payPalRefundId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        PayPalRefundId = payPalRefundId;
        Amount = amount;
        Status = status;
        IdempotencyKey = idempotencyKey;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public int PaymentId { get; private set; }
    public string PayPalRefundId { get; private set; }
    public decimal Amount { get; private set; }
    public string Status { get; private set; }        // raw PayPal wire string
    public string IdempotencyKey { get; private set; } // caller-supplied
    public DateTimeOffset CreatedAt { get; private set; }
}
