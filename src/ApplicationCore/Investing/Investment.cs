using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// A single investment of a shopper's accumulated spare change into the
/// configured fund at Upvest.
/// </summary>
public class Investment : BaseEntity, IAggregateRoot
{
    public string BuyerId { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public InvestmentStatus Status { get; private set; }
    public Guid? UpvestOrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Investment() { }

    public Investment(string buyerId, decimal amount)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        BuyerId = buyerId;
        Amount = amount;
        Status = InvestmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void AttachOrder(Guid upvestOrderId) => UpvestOrderId = upvestOrderId;
    public void MarkSettled() => Status = InvestmentStatus.Settled;
    public void MarkFailed() => Status = InvestmentStatus.Failed;
}
