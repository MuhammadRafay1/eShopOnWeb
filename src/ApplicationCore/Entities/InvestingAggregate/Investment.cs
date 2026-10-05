using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's accumulated change into the configured fund. Tracks the money
/// committed, the Upvest order placed for it, and where that order's outcome has got to.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(string buyerId, long amountCents)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        AmountCents = Guard.Against.NegativeOrZero(amountCents, nameof(amountCents));
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        // A stable key reused across retries of the same logical write (idempotency / reconciliation).
        OrderIdempotencyKey = Guid.NewGuid();
        FundingIdempotencyKey = Guid.NewGuid();
        ClientReference = Guid.NewGuid().ToString("N");
    }

    public string BuyerId { get; private set; }
    public long AmountCents { get; private set; }
    public InvestmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The Upvest order id, once the buy order has been accepted. Null until then.</summary>
    public string? UpvestOrderId { get; private set; }

    /// <summary>True once the account group has been funded with this investment's amount.</summary>
    public bool Funded { get; private set; }

    /// <summary>Correlates the placed order back to this investment for outcome reconciliation.</summary>
    public string ClientReference { get; private set; }

    public Guid OrderIdempotencyKey { get; private set; }
    public Guid FundingIdempotencyKey { get; private set; }

    public void MarkFunded() => Funded = true;

    public void RecordOrder(string upvestOrderId) =>
        UpvestOrderId = Guard.Against.NullOrEmpty(upvestOrderId, nameof(upvestOrderId));

    public void Settle() => Status = InvestmentStatus.Settled;

    public void Fail() => Status = InvestmentStatus.Failed;
}
