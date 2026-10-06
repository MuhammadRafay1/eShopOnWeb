using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A single investment of a shopper's set-aside change into the configured fund at Upvest,
/// backed by one Upvest order.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investment() { }
#pragma warning restore CS8618

    public Investment(string buyerId, int investorId, decimal amount)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        BuyerId = buyerId;
        InvestorId = investorId;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The shopper this investment belongs to (their identity from the JWT).</summary>
    public string BuyerId { get; private set; }

    public int InvestorId { get; private set; }

    /// <summary>Amount invested, in euros.</summary>
    public decimal Amount { get; private set; }

    public string? UpvestOrderId { get; private set; }

    public InvestmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void LinkUpvestOrder(string orderId)
    {
        Guard.Against.NullOrEmpty(orderId, nameof(orderId));
        UpvestOrderId = orderId;
    }

    public void MarkSettled() => Status = InvestmentStatus.Settled;

    public void MarkFailed() => Status = InvestmentStatus.Failed;
}
