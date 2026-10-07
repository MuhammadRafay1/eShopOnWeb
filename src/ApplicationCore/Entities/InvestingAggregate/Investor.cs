using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Holds the link
/// to the shopper's identity at Upvest, the change set aside but not yet
/// invested, and the history of investments made on their behalf.
///
/// A shopper's enrolment, ledger and investments belong to that shopper alone:
/// every query is scoped by <see cref="BuyerId"/>.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>The whole set-aside balance is invested once it reaches this many euros.</summary>
    public const decimal InvestmentThreshold = 10m;

    /// <summary>Stable, externally shared identifier for the enrolment.</summary>
    public Guid EnrolmentId { get; private set; } = Guid.NewGuid();

    /// <summary>The shopper this enrolment belongs to (the PublicApi buyer id / username).</summary>
    public string BuyerId { get; private set; }

    /// <summary>The shopper's user id at Upvest.</summary>
    public string ProviderUserId { get; private set; }

    /// <summary>The Upvest account that holds the shopper's investments; set once provisioned.</summary>
    public string? ProviderAccountId { get; private set; }

    public EnrolmentStatus Status { get; private set; } = EnrolmentStatus.Pending;

    /// <summary>Change set aside and not yet invested, in euros.</summary>
    public decimal PendingAmount { get; private set; }

    private readonly List<Investment> _investments = new();
    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    /// <summary>The total euro amount invested on the shopper's behalf so far.</summary>
    public decimal InvestedAmount => _investments.Sum(i => i.Amount);

    private Investor() { } // EF

    public Investor(string buyerId, string providerUserId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(providerUserId, nameof(providerUserId));
        BuyerId = buyerId;
        ProviderUserId = providerUserId;
    }

    public void MarkActive() => Status = EnrolmentStatus.Active;
    public void MarkRejected() => Status = EnrolmentStatus.Rejected;
    public void MarkPending() => Status = EnrolmentStatus.Pending;

    public void SetProviderAccount(string providerAccountId)
    {
        Guard.Against.NullOrEmpty(providerAccountId, nameof(providerAccountId));
        ProviderAccountId = providerAccountId;
    }

    /// <summary>True once Upvest has accepted the shopper and an account exists to hold investments.</summary>
    public bool CanInvest => Status == EnrolmentStatus.Active && !string.IsNullOrEmpty(ProviderAccountId);

    /// <summary>
    /// Sets aside the rounding difference from a paid order. Only an accepted
    /// investor accrues change.
    /// </summary>
    public void SetAside(decimal roundUpAmount)
    {
        if (roundUpAmount <= 0m || !CanInvest)
        {
            return;
        }

        PendingAmount += roundUpAmount;
    }

    /// <summary>Whether the set-aside balance has reached the investment threshold.</summary>
    public bool IsReadyToInvest => PendingAmount >= InvestmentThreshold;

    /// <summary>
    /// Records that the whole current set-aside balance has been invested via
    /// the given Upvest order, and resets the balance to zero. Returns the new
    /// investment. Call only after the order has been accepted at Upvest.
    /// </summary>
    public Investment RecordInvestment(string providerOrderId)
    {
        Guard.Against.NullOrEmpty(providerOrderId, nameof(providerOrderId));
        var amount = PendingAmount;
        var investment = new Investment(amount, providerOrderId);
        _investments.Add(investment);
        PendingAmount = 0m;
        return investment;
    }
}
