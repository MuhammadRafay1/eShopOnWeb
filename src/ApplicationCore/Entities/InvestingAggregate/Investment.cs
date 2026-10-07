using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single amount invested on a shopper's behalf into the configured fund. Part of the
/// <see cref="Investor"/> aggregate. Amounts are held in whole euro cents to keep the ledger exact.
/// </summary>
public class Investment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }

    public Investment(Guid upvestOrderId, long amountCents, InvestmentStatus status)
    {
        UpvestOrderId = upvestOrderId;
        AmountCents = amountCents;
        Status = status;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The Upvest order this investment was placed as.</summary>
    public Guid UpvestOrderId { get; private set; }

    /// <summary>The amount invested, in euro cents.</summary>
    public long AmountCents { get; private set; }

    public InvestmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The amount invested, in euros.</summary>
    public decimal Amount => AmountCents / 100m;

    internal void SetStatus(InvestmentStatus status) => Status = status;
}
