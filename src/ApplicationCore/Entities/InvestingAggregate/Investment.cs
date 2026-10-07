using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of accrued spare change into the configured fund, held on the
/// shopper's behalf at Upvest.
/// </summary>
public class Investment : BaseEntity
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(decimal amount, string instrumentId, string upvestOrderId)
    {
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Guard.Against.NullOrEmpty(upvestOrderId, nameof(upvestOrderId));

        PublicId = Guid.NewGuid();
        Amount = amount;
        InstrumentId = instrumentId;
        UpvestOrderId = upvestOrderId;
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable, non-sequential identifier exposed to API callers.</summary>
    public Guid PublicId { get; private set; }

    /// <summary>The amount invested, in euros.</summary>
    public decimal Amount { get; private set; }

    /// <summary>The Upvest instrument (fund) the money was invested in.</summary>
    public string InstrumentId { get; private set; }

    /// <summary>The Upvest order that carries out this investment.</summary>
    public string UpvestOrderId { get; private set; }

    public InvestmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Records that Upvest settled this investment.</summary>
    public void MarkSettled() => Status = InvestmentStatus.Settled;

    /// <summary>Records that this investment failed, was cancelled or rejected at Upvest.</summary>
    public void MarkFailed() => Status = InvestmentStatus.Failed;
}
