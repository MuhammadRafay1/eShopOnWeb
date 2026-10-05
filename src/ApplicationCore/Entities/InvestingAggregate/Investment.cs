using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's accumulated set-aside change into the configured fund.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
    /// <summary>Stable, externally-facing investment id.</summary>
    public Guid InvestmentId { get; private set; } = Guid.NewGuid();

    public int InvestorId { get; private set; }

    /// <summary>Amount (EUR) being invested.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; } = InvestmentStatus.Pending;

    /// <summary>The Upvest order id once the buy order has been placed.</summary>
    public string? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedDate { get; private set; } = DateTimeOffset.UtcNow;

    #pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }

    public Investment(int investorId, decimal amount)
    {
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        InvestorId = investorId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>True until the buy order has actually been placed at Upvest.</summary>
    public bool IsAwaitingPlacement => UpvestOrderId is null && Status == InvestmentStatus.Pending;

    public void MarkPlaced(string upvestOrderId)
    {
        Guard.Against.NullOrEmpty(upvestOrderId, nameof(upvestOrderId));
        UpvestOrderId = upvestOrderId;
    }

    public void MarkSettled() => Status = InvestmentStatus.Settled;

    public void MarkFailed() => Status = InvestmentStatus.Failed;
}
