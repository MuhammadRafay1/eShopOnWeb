using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's accumulated spare change into the
/// configured fund, backed by one order at Upvest.
/// </summary>
public class Investment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }

    public Investment(decimal amount, string upvestOrderId)
    {
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(upvestOrderId, nameof(upvestOrderId));

        Amount = amount;
        UpvestOrderId = upvestOrderId;
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public int InvestorId { get; private set; }

    /// <summary>The amount invested, in euros.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; }

    /// <summary>The id of the backing order at Upvest.</summary>
    public string UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public void MarkSettled()
    {
        if (Status == InvestmentStatus.Pending)
        {
            Status = InvestmentStatus.Settled;
            SettledAt = DateTimeOffset.UtcNow;
        }
    }

    public void MarkFailed()
    {
        if (Status == InvestmentStatus.Pending)
        {
            Status = InvestmentStatus.Failed;
        }
    }
}
