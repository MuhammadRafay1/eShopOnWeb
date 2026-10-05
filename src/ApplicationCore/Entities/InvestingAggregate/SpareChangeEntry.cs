using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// The spare change set aside from a single paid order (the amount rounded up to the
/// next whole euro). Kept as an immutable audit trail and to make setting aside
/// idempotent per order. Part of the <see cref="InvestingAccount"/> aggregate.
/// </summary>
public class SpareChangeEntry : BaseEntity
{
#pragma warning disable CS8618 // Required by Entity Framework
    private SpareChangeEntry() { }
#pragma warning restore CS8618

    internal SpareChangeEntry(int orderId, decimal amount, decimal orderTotal)
    {
        OrderId = orderId;
        Amount = amount;
        OrderTotal = orderTotal;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The shop order this change came from.</summary>
    public int OrderId { get; private set; }

    /// <summary>The amount, in euros, set aside from the order.</summary>
    public decimal Amount { get; private set; }

    /// <summary>The order total the round-up was computed from.</summary>
    public decimal OrderTotal { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
