using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's accumulated spare change into the configured fund. Carries the
/// idempotency keys and client reference used to talk to Upvest exactly once, and mirrors the order's
/// outcome at Upvest in <see cref="Status"/>.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(int investorId, long amountCents)
    {
        Guard.Against.NegativeOrZero(amountCents, nameof(amountCents));
        InvestorId = investorId;
        AmountCents = amountCents;
        Status = InvestmentStatus.Pending;
        ClientReference = $"eshop-inv-{Guid.NewGuid():N}";
        TopupIdempotencyKey = Guid.NewGuid();
        OrderIdempotencyKey = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public int InvestorId { get; private set; }
    public long AmountCents { get; private set; }
    public InvestmentStatus Status { get; private set; }

    /// <summary>A unique reference sent with the Upvest order so the order can be found again after an uncertain write.</summary>
    public string ClientReference { get; private set; }

    public string? UpvestOrderId { get; private set; }
    public bool Funded { get; private set; }

    public Guid TopupIdempotencyKey { get; private set; }
    public Guid OrderIdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void MarkFunded() { Funded = true; Touch(); }
    public void LinkOrder(string upvestOrderId) { UpvestOrderId = upvestOrderId; Touch(); }
    public void MarkSettled() { Status = InvestmentStatus.Settled; Touch(); }
    public void MarkFailed() { Status = InvestmentStatus.Failed; Touch(); }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
