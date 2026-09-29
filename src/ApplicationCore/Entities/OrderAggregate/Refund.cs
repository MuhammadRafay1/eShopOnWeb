using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// A single refund issued against an <see cref="OrderPayment"/>'s capture. A capture may have
/// several refunds (multiple partial refunds), each carrying the caller-supplied idempotency
/// key that produced it so a replay under the same key returns the same row rather than
/// refunding twice.
/// </summary>
public class Refund : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Refund() { }

    public Refund(string payPalRefundId, decimal amount, string currencyCode, string status, string idempotencyKey)
    {
        Guard.Against.NullOrEmpty(payPalRefundId, nameof(payPalRefundId));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));
        Guard.Against.NullOrEmpty(status, nameof(status));
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));
        Guard.Against.Negative(amount, nameof(amount));

        PayPalRefundId = payPalRefundId;
        Amount = amount;
        CurrencyCode = currencyCode;
        Status = status;
        IdempotencyKey = idempotencyKey;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public int OrderPaymentId { get; private set; }

    /// <summary>PayPal's own refund id (from POST /v2/payments/captures/{id}/refund).</summary>
    public string PayPalRefundId { get; private set; }

    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; }

    /// <summary>PayPal refund_status: CANCELLED | FAILED | PENDING | COMPLETED.</summary>
    public string Status { get; private set; }

    /// <summary>Caller-supplied idempotency key that produced this refund.</summary>
    public string IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
