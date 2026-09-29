using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// A single refund against a <see cref="Payment"/>'s capture. Multiple partial refunds against
/// the same capture are legitimate; each carries the caller-supplied idempotency key so a
/// repeated request under the same key is served from the stored result rather than refunding
/// twice (unique per Payment - see RefundConfiguration).
/// </summary>
public class Refund : BaseEntity
{
    public int PaymentId { get; private set; }
    public string PayPalRefundId { get; private set; }
    public string IdempotencyKey { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>Mirrors PayPal's refund_status string directly (COMPLETED, PENDING, ...).</summary>
    public string Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private Refund() { }
#pragma warning restore CS8618

    internal Refund(string payPalRefundId, string idempotencyKey, decimal amount, string status)
    {
        PayPalRefundId = payPalRefundId;
        IdempotencyKey = idempotencyKey;
        Amount = amount;
        Status = status;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
