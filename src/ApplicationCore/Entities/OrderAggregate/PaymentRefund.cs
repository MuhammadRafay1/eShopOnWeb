using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

public enum RefundState
{
    /// <summary>Amount reserved and the refund sent (or about to be sent); outcome not yet confirmed.</summary>
    Requested = 0,
    Pending = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>A refund of (part of) a captured payment, keyed by the caller's idempotency key.</summary>
public class PaymentRefund : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentRefund() { }

    internal PaymentRefund(string idempotencyKey, decimal amount, string currency, DateTimeOffset requestedAt)
    {
        IdempotencyKey = idempotencyKey;
        Amount = amount;
        Currency = currency;
        RequestedAt = requestedAt;
        State = RefundState.Requested;
    }

    public int OrderPaymentId { get; private set; }
    public string IdempotencyKey { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public RefundState State { get; private set; }
    public string? RefundId { get; private set; }
    public string? ProviderStatus { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    internal void RecordProviderResult(string refundId, string? providerStatus, RefundState state,
        decimal? amount, DateTimeOffset? at)
    {
        RefundId = refundId;
        ProviderStatus = providerStatus;
        State = state;
        if (amount is not null) Amount = amount.Value;
        CompletedAt = at;
        FailureReason = null;
    }

    internal void MarkFailed(string reason)
    {
        State = RefundState.Failed;
        FailureReason = reason;
    }

    internal void MarkOutcomeUnknown(string reason) => FailureReason = reason;
}
