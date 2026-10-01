using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// A single refund made against an order's captured payment. Child of <see cref="OrderPayment"/>.
/// The <see cref="IdempotencyKey"/> (the caller-supplied key, prefixed with the order id) is unique
/// per capture, so repeating a refund request under the same key returns this same row rather than
/// issuing a second refund.
/// </summary>
public class OrderRefund : BaseEntity
{
#pragma warning disable CS8618 // Required by Entity Framework
    private OrderRefund() { }
#pragma warning restore CS8618

    public OrderRefund(string idempotencyKey, decimal amount, string currency)
    {
        IdempotencyKey = idempotencyKey;
        Amount = amount;
        Currency = currency;
        CreatedAt = DateTimeOffset.UtcNow;
        Status = "PENDING";
    }

    public int OrderPaymentId { get; private set; }
    public string IdempotencyKey { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string? PayPalRefundId { get; private set; }

    /// <summary>The raw PayPal refund status (e.g. <c>COMPLETED</c>, <c>PENDING</c>, <c>FAILED</c>).</summary>
    public string Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkSucceeded(string payPalRefundId, string status)
    {
        PayPalRefundId = payPalRefundId;
        Status = status;
    }

    public void MarkFailed()
    {
        Status = "FAILED";
    }

    /// <summary>
    /// A refund counts against the captured total unless it is known to have failed. Pending and
    /// completed refunds both reserve the amount so a later refund cannot over-refund the capture.
    /// </summary>
    public bool CountsTowardRefundedTotal =>
        !string.Equals(Status, "FAILED", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);
}
