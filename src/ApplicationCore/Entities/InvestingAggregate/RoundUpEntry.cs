using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// One line of the shopper's spare-change ledger: the amount a single paid order set aside.
/// </summary>
public class RoundUpEntry : BaseEntity
{
#pragma warning disable CS8618 // Required by Entity Framework
    private RoundUpEntry() { }
#pragma warning restore CS8618

    public RoundUpEntry(int orderId, decimal amount)
    {
        OrderId = orderId;
        Amount = amount;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The shop order whose payment produced this round-up.</summary>
    public int OrderId { get; private set; }

    /// <summary>The amount set aside from that order, in euros.</summary>
    public decimal Amount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
