using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// One refund request, keyed by the caller-supplied idempotency key so a repeated request under
/// the same key cannot refund twice (see DUPLICATE CLAIMS in the build plan).
/// </summary>
public class PaymentRefund : IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentRefund() { }

    public PaymentRefund(string idempotencyKey, int orderId, string captureId, decimal amount)
    {
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));

        IdempotencyKey = idempotencyKey;
        OrderId = orderId;
        CaptureId = captureId;
        Amount = amount;
        Status = "Pending";
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Primary key - the caller-supplied idempotency key for this refund request.</summary>
    public string IdempotencyKey { get; private set; }

    public int OrderId { get; private set; }
    public string CaptureId { get; private set; }
    public decimal Amount { get; private set; }
    public string? PayPalRefundId { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Completed(string payPalRefundId, string status)
    {
        PayPalRefundId = payPalRefundId;
        Status = status;
    }
}
