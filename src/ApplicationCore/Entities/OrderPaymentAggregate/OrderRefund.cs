using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderPaymentAggregate;

/// <summary>
/// One PayPal refund against an OrderPayment's capture. Several of these can exist per OrderPayment
/// (partial refunds); each is keyed by the caller-supplied idempotency key that produced it.
/// </summary>
public class OrderRefund : BaseEntity
{
#pragma warning disable CS8618 // Required by Entity Framework
    private OrderRefund() { }

    public OrderRefund(string payPalRefundId, decimal amount, string idempotencyKey, string status)
    {
        Guard.Against.NullOrEmpty(payPalRefundId, nameof(payPalRefundId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));
        Guard.Against.NullOrEmpty(status, nameof(status));

        PayPalRefundId = payPalRefundId;
        Amount = amount;
        IdempotencyKey = idempotencyKey;
        Status = status;
        CreatedDate = DateTimeOffset.UtcNow;
    }

    public string PayPalRefundId { get; private set; }
    public decimal Amount { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset CreatedDate { get; private set; }
}
