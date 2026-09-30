using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A single refund issued against a <see cref="Payment"/>'s capture. Reached only through the
/// owning Payment aggregate.
/// </summary>
public class Refund : BaseEntity
{
    public int PaymentId { get; private set; }
    public string RefundId { get; private set; }
    public decimal Amount { get; private set; }
    public string Status { get; private set; }
    public string IdempotencyKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private Refund() { }

    public Refund(int paymentId, string refundId, decimal amount, string status, string idempotencyKey)
    {
        Guard.Against.NegativeOrZero(paymentId, nameof(paymentId));
        Guard.Against.NullOrEmpty(refundId, nameof(refundId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(status, nameof(status));
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        PaymentId = paymentId;
        RefundId = refundId;
        Amount = amount;
        Status = status;
        IdempotencyKey = idempotencyKey;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
