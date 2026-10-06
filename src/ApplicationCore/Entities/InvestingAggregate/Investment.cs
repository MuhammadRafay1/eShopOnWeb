using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment made on a shopper's behalf: the whole set-aside balance invested in the
/// configured fund via one Upvest order. Lives inside the <see cref="Investor"/> aggregate.
/// </summary>
public class Investment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }

    internal Investment(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Investment amount must be positive.");
        PublicId = Guid.NewGuid();
        Amount = decimal.Round(amount, 2);
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable, externally shared identifier for this investment.</summary>
    public Guid PublicId { get; private set; }

    /// <summary>The amount, in euros, invested in this single investment.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; }

    /// <summary>The id of the Upvest order that carries out this investment.</summary>
    public string? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal void MarkOrderPlaced(string upvestOrderId, InvestmentStatus status)
    {
        UpvestOrderId = upvestOrderId;
        Status = status;
    }

    internal void ApplyStatus(InvestmentStatus status) => Status = status;
}
