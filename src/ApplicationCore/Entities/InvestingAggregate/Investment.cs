using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's accumulated spare change into the configured fund.
/// Belongs to exactly one shopper.
/// </summary>
public class Investment : IAggregateRoot
{
    public Guid Id { get; private set; } // = the public investmentId

    public string ShopperId { get; private set; }

    /// <summary>Amount invested, in euro cents.</summary>
    public long AmountCents { get; private set; }

    public InvestmentStage Stage { get; private set; }

    public Guid? UpvestTopupId { get; private set; }
    public Guid? UpvestOrderId { get; private set; }

    // Stable idempotency keys so a worker retry replays at Upvest rather than funding/ordering twice.
    public Guid TopupIdempotencyKey { get; private set; }
    public Guid OrderIdempotencyKey { get; private set; }

    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(string shopperId, long amountCents)
    {
        Id = Guid.NewGuid();
        ShopperId = shopperId;
        AmountCents = amountCents;
        Stage = InvestmentStage.Funding;
        TopupIdempotencyKey = Guid.NewGuid();
        OrderIdempotencyKey = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public InvestmentStatus Status => Stage switch
    {
        InvestmentStage.Settled => InvestmentStatus.Settled,
        InvestmentStage.Failed => InvestmentStatus.Failed,
        _ => InvestmentStatus.Pending
    };

    public bool IsTerminal => Stage is InvestmentStage.Settled or InvestmentStage.Failed;

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void RecordTopup(Guid topupId) { UpvestTopupId = topupId; Touch(); }

    public void MarkFunded()
    {
        if (Stage == InvestmentStage.Funding) { Stage = InvestmentStage.AwaitingFunds; Touch(); }
    }

    public void MarkPlacing() { Stage = InvestmentStage.Placing; Touch(); }

    public void RecordOrder(Guid orderId)
    {
        UpvestOrderId = orderId;
        Stage = InvestmentStage.AwaitingFill;
        Touch();
    }

    public void MarkSettled() { Stage = InvestmentStage.Settled; LastError = null; Touch(); }

    public void MarkFailed(string reason) { Stage = InvestmentStage.Failed; LastError = Truncate(reason); Touch(); }

    public void RecordTransientError(string reason) { LastError = Truncate(reason); Touch(); }

    private static string Truncate(string s) => s.Length > 500 ? s[..500] : s;
}
