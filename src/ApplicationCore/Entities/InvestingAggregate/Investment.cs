using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment made on a shopper's behalf: the whole set-aside balance invested in the
/// configured fund once it reached the threshold. Its <see cref="Status"/> mirrors the outcome of the
/// backing order at Upvest.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(int investorId, decimal amount)
    {
        InvestorId = Guard.Against.NegativeOrZero(investorId, nameof(investorId));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The investor this investment belongs to.</summary>
    public int InvestorId { get; private set; }

    /// <summary>How much was invested, in euros.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; }

    /// <summary>The Upvest order that carries out this investment.</summary>
    public string? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void LinkUpvestOrder(string upvestOrderId) =>
        UpvestOrderId = Guard.Against.NullOrEmpty(upvestOrderId, nameof(upvestOrderId));

    public void Settle() => Status = InvestmentStatus.Settled;

    public void Fail() => Status = InvestmentStatus.Failed;
}
