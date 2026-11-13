using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The lifecycle of a pay-in, as reflected by Upvest. <see cref="Pending"/> until the payment's
/// outcome at Upvest is known, then <see cref="Invested"/> or <see cref="Failed"/>.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Invested = 1,
    Failed = 2
}

/// <summary>
/// A shopper's pay-in: money credited to their Upvest account and invested across the funds of
/// their chosen model in that model's weights. Keyed by the application-generated
/// <see cref="PaymentId"/>; this row is written as a claim before any Upvest call, so the ordering
/// is claim → Upvest call → record the result.
/// </summary>
public class Payment : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
#pragma warning restore CS8618

    public Payment(Guid paymentId, string shopperId, decimal amount)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));

        PaymentId = paymentId;
        ShopperId = shopperId;
        Amount = decimal.Round(amount, 2, MidpointRounding.ToZero);
        Status = PaymentStatus.Pending;
        CreatedDate = DateTimeOffset.UtcNow;

        TopupIdempotencyKey = Guid.NewGuid();
        OrderIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>Application-facing payment id. Primary key.</summary>
    public Guid PaymentId { get; private set; }

    /// <summary>The shopper this payment belongs to (JWT name claim). Never exposed to other shoppers.</summary>
    public string ShopperId { get; private set; }

    /// <summary>The amount paid in, in euros (two decimal places).</summary>
    public decimal Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    public Guid? UpvestTopupId { get; private set; }
    public Guid? UpvestOrderId { get; private set; }

    public Guid TopupIdempotencyKey { get; private set; }
    public Guid OrderIdempotencyKey { get; private set; }

    public DateTimeOffset CreatedDate { get; private set; }

    /// <summary>A short, non-personal note about the last outcome.</summary>
    public string? Note { get; private set; }

    public void SetTopupId(Guid topupId) => UpvestTopupId = topupId;
    public void SetOrderId(Guid orderId) => UpvestOrderId = orderId;
    public void SetStatus(PaymentStatus status) => Status = status;
    public void SetNote(string? note) => Note = note;
}
