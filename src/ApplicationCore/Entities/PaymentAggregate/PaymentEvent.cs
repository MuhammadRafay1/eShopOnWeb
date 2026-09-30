using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

public enum PaymentEventType
{
    Authorized = 0,
    Reauthorized = 1,
    Captured = 2,
    Voided = 3,
    Refunded = 4
}

/// <summary>
/// Append-only audit entry recording each state change of a <see cref="Payment"/>. Lets an operator
/// (and the reconciliation report) explain why a payment is in its current state, including
/// intermediate reauthorizations that the "current state" fields overwrite. This is a log, not a
/// second source of truth.
/// </summary>
public class PaymentEvent : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentEvent() { }

    public PaymentEvent(PaymentEventType eventType, string? payPalId, string? status)
    {
        EventType = eventType;
        PayPalId = payPalId;
        Status = status;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public int PaymentId { get; private set; }
    public PaymentEventType EventType { get; private set; }
    public string? PayPalId { get; private set; }
    public string? Status { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
