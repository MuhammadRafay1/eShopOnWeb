using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// A shopper's running set-aside balance: spare change rounded up from paid orders that has not
/// yet been invested. Amounts are euros, held to two decimal places.
/// </summary>
public class SpareChangeLedger : BaseEntity, IAggregateRoot
{
    public string ShopperId { get; private set; }

    /// <summary>Amount set aside and not yet invested.</summary>
    public decimal PendingAmount { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private SpareChangeLedger() { }
#pragma warning restore CS8618

    public SpareChangeLedger(string shopperId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        ShopperId = shopperId;
        PendingAmount = 0m;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddRoundUp(decimal amount)
    {
        Guard.Against.Negative(amount, nameof(amount));
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Removes the whole balance so it can be invested, returning the amount withdrawn.</summary>
    public decimal WithdrawAll()
    {
        var amount = PendingAmount;
        PendingAmount = 0m;
        UpdatedAt = DateTimeOffset.UtcNow;
        return amount;
    }

    /// <summary>Returns a failed investment's amount to the balance so it accrues towards the next one.</summary>
    public void Restore(decimal amount)
    {
        Guard.Against.Negative(amount, nameof(amount));
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
