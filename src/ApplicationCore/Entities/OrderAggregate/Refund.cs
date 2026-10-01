using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// A single refund against a captured <see cref="Payment"/>. The <see cref="IdempotencyKey"/> is the
/// caller-supplied key: repeating a refund request under the same key must not refund twice, while two
/// distinct keys against the same capture are two legitimate partial refunds.
/// </summary>
public class Refund : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Refund() { }

    public Refund(string idempotencyKey, decimal amount, string currencyCode)
    {
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));

        IdempotencyKey = idempotencyKey;
        Amount = amount;
        CurrencyCode = currencyCode;
        Status = "PENDING";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public int PaymentId { get; private set; }

    /// <summary>Caller-supplied idempotency key, unique per <see cref="PaymentId"/>.</summary>
    public string IdempotencyKey { get; private set; }

    /// <summary>PayPal's refund id; null until PayPal has answered.</summary>
    public string? PayPalRefundId { get; private set; }

    /// <summary>Raw PayPal RefundStatus wire value (e.g. COMPLETED / PENDING / FAILED / CANCELLED).</summary>
    public string? Status { get; private set; }

    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Record the PayPal outcome once the refund call returns.</summary>
    public void RecordResult(string payPalRefundId, string status)
    {
        Guard.Against.NullOrEmpty(payPalRefundId, nameof(payPalRefundId));
        PayPalRefundId = payPalRefundId;
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>True when PayPal considers this refund money-moving (completed or still pending settlement).</summary>
    public bool CountsTowardRefundedTotal =>
        !string.Equals(Status, "FAILED", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);
}
