using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// Audit record of the spare change a single paid order set aside. One per eShop order,
/// so the same order is never set aside twice. Amount is in euro cents (0 is not recorded).
/// </summary>
public class RoundUp : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private RoundUp() { }
#pragma warning restore CS8618

    public RoundUp(string buyerId, int orderId, long amountCents)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NegativeOrZero(amountCents, nameof(amountCents));
        BuyerId = buyerId;
        OrderId = orderId;
        AmountCents = amountCents;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string BuyerId { get; private set; }

    /// <summary>The eShop order that was paid and rounded up.</summary>
    public int OrderId { get; private set; }

    public long AmountCents { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
