using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// One investment of a shopper's accrued spare change into the configured fund, tracked
/// against the Upvest order it was placed as. Amount is in euro cents.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(string buyerId, long amountCents)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NegativeOrZero(amountCents, nameof(amountCents));
        BuyerId = buyerId;
        AmountCents = amountCents;
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string BuyerId { get; private set; }
    public long AmountCents { get; private set; }
    public InvestmentStatus Status { get; private set; }

    /// <summary>The Upvest order this investment was placed as, once placement succeeds.</summary>
    public Guid? UpvestOrderId { get; private set; }

    /// <summary>The instrument (ISIN) it was invested in.</summary>
    public string? InstrumentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void LinkUpvestOrder(Guid orderId, string instrumentId)
    {
        UpvestOrderId = orderId;
        InstrumentId = instrumentId;
        Touch();
    }

    public void MarkSettled()
    {
        if (Status == InvestmentStatus.Settled) return;
        Status = InvestmentStatus.Settled;
        Touch();
    }

    public void MarkFailed()
    {
        if (Status == InvestmentStatus.Failed) return;
        Status = InvestmentStatus.Failed;
        Touch();
    }

    public bool IsPending => Status == InvestmentStatus.Pending;

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
