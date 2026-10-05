using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// A single investment of a shopper's set-aside balance into the configured fund at Upvest.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
    public Guid InvestmentId { get; private set; }

    public string ShopperId { get; private set; }

    /// <summary>Amount invested, in euros.</summary>
    public decimal Amount { get; private set; }

    public InvestmentStatus Status { get; private set; }

    /// <summary>The Upvest order id, once the buy order has been placed.</summary>
    public string? UpvestOrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(string shopperId, decimal amount)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        ShopperId = shopperId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        InvestmentId = Guid.NewGuid();
        Status = InvestmentStatus.Created;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void MarkPlaced(string upvestOrderId)
    {
        Guard.Against.NullOrEmpty(upvestOrderId, nameof(upvestOrderId));
        UpvestOrderId = upvestOrderId;
        Status = InvestmentStatus.Placed;
        Touch();
    }

    public void MarkSettled()
    {
        Status = InvestmentStatus.Settled;
        Touch();
    }

    public void MarkFailed()
    {
        Status = InvestmentStatus.Failed;
        Touch();
    }

    public bool IsTerminal => Status == InvestmentStatus.Settled || Status == InvestmentStatus.Failed;

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
