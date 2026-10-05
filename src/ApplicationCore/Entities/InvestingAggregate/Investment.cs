using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's set-aside change into the configured fund.
/// Part of the <see cref="Investor"/> aggregate.
/// </summary>
public class Investment : BaseEntity
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(long amountCents)
    {
        if (amountCents <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountCents), "An investment must be a positive amount.");

        InvestmentId = Guid.NewGuid();
        AmountCents = amountCents;
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>Stable public identifier, surfaced as <c>investmentId</c>.</summary>
    public Guid InvestmentId { get; private set; }

    /// <summary>Amount invested, in euro cents.</summary>
    public long AmountCents { get; private set; }

    public InvestmentStatus Status { get; private set; }

    /// <summary>The Upvest order id this investment was placed as.</summary>
    public string? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public decimal Amount => AmountCents / 100m;

    public void LinkUpvestOrder(string upvestOrderId)
    {
        UpvestOrderId = upvestOrderId;
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

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
