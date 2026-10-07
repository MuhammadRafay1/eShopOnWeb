using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Owns the shopper's enrolment
/// state, their not-yet-invested balance (the spare-change ledger) and their investments.
/// Everything here belongs to exactly one shopper, identified by <see cref="BuyerId"/>.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>The balance at which the accrued spare change is invested.</summary>
    public const decimal InvestmentThreshold = 10m;

#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string buyerId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        PublicId = Guid.NewGuid();
        BuyerId = buyerId;
        Status = InvestorStatus.Pending;
        PendingAmount = 0m;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable, non-sequential identifier exposed to API callers as the enrolment id.</summary>
    public Guid PublicId { get; private set; }

    /// <summary>The shopper this investor belongs to (their sign-in identity / username).</summary>
    public string BuyerId { get; private set; }

    /// <summary>The user Upvest holds for this shopper, once created.</summary>
    public string? UpvestUserId { get; private set; }

    /// <summary>The Upvest account the shopper's investments are held in, once created.</summary>
    public string? UpvestAccountId { get; private set; }

    public InvestorStatus Status { get; private set; }

    /// <summary>Spare change set aside but not yet invested, in euros.</summary>
    public decimal PendingAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<Investment> _investments = new();
    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    private readonly List<RoundUpEntry> _ledger = new();
    public IReadOnlyCollection<RoundUpEntry> Ledger => _ledger.AsReadOnly();

    /// <summary>True once Upvest has accepted the shopper and they may invest.</summary>
    public bool IsActive => Status == InvestorStatus.Active;

    /// <summary>The total that has been committed to investments that have not failed, in euros.</summary>
    public decimal InvestedAmount =>
        _investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.Amount);

    /// <summary>Records the Upvest user created for this shopper while enrolment is still pending.</summary>
    public void LinkUpvestUser(string upvestUserId, string? upvestAccountId)
    {
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        UpvestUserId = upvestUserId;
        UpvestAccountId = upvestAccountId;
    }

    public void SetUpvestAccount(string upvestAccountId)
    {
        Guard.Against.NullOrEmpty(upvestAccountId, nameof(upvestAccountId));
        UpvestAccountId = upvestAccountId;
    }

    public void MarkActive() => Status = InvestorStatus.Active;

    public void MarkRejected() => Status = InvestorStatus.Rejected;

    /// <summary>
    /// Sets aside the round-up from one paid order. Only an accepted investor accrues change,
    /// and a zero round-up (an order already at a whole euro) is a no-op.
    /// </summary>
    /// <returns>The amount actually set aside.</returns>
    public decimal SetAside(int orderId, decimal roundUp)
    {
        if (!IsActive || roundUp <= 0m)
        {
            return 0m;
        }

        _ledger.Add(new RoundUpEntry(orderId, roundUp));
        PendingAmount += roundUp;
        return roundUp;
    }

    /// <summary>True when the accrued balance has reached the investment threshold.</summary>
    public bool IsReadyToInvest => IsActive && PendingAmount >= InvestmentThreshold;

    /// <summary>
    /// Moves the whole accrued balance into a new investment once an Upvest order has been
    /// placed for it. The pending balance then starts again from zero.
    /// </summary>
    public Investment BeginInvestment(string instrumentId, string upvestOrderId)
    {
        Guard.Against.NullOrEmpty(instrumentId, nameof(instrumentId));
        Guard.Against.NullOrEmpty(upvestOrderId, nameof(upvestOrderId));
        if (PendingAmount <= 0m)
        {
            throw new InvalidOperationException("There is no accrued balance to invest.");
        }

        var investment = new Investment(PendingAmount, instrumentId, upvestOrderId);
        _investments.Add(investment);
        PendingAmount = 0m;
        return investment;
    }
}
