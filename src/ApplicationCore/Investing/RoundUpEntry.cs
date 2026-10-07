using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The spare change set aside from a single paid order: the difference between
/// the order total and the next whole euro.
/// </summary>
public class RoundUpEntry : BaseEntity, IAggregateRoot
{
    public string BuyerId { get; private set; } = default!;
    public int OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private RoundUpEntry() { }

    public RoundUpEntry(string buyerId, int orderId, decimal amount)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Negative(amount, nameof(amount));
        BuyerId = buyerId;
        OrderId = orderId;
        Amount = amount;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
