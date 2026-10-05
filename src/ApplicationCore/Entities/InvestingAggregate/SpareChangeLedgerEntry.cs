using System;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// One line in a shopper's spare-change ledger: the amount a single paid order
/// set aside towards their next investment.
/// </summary>
public class SpareChangeLedgerEntry : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private SpareChangeLedgerEntry() { }

    public SpareChangeLedgerEntry(int orderId, decimal amount)
    {
        Guard.Against.NegativeOrZero(amount, nameof(amount));

        OrderId = orderId;
        Amount = amount;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public int InvestorId { get; private set; }

    /// <summary>The eShop order whose payment set this amount aside.</summary>
    public int OrderId { get; private set; }

    /// <summary>The amount set aside by that order, in euros.</summary>
    public decimal Amount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
