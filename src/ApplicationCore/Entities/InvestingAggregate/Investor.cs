using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Aggregate root.
/// Holds only the Upvest linkage and the spare-change ledger — never any personal detail,
/// which is passed straight to Upvest at enrolment and never persisted or logged here.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>Threshold (in cents) at which the set-aside balance is invested.</summary>
    public const long InvestmentThresholdCents = 1000; // €10.00

    #pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }

    public Investor(string buyerId)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        EnrolmentId = Guid.NewGuid();
        Status = EnrolmentStatus.Pending;
    }

    /// <summary>Stable identifier surfaced to API callers as <c>enrolmentId</c>.</summary>
    public Guid EnrolmentId { get; private set; }

    /// <summary>The owning shopper (the PublicApi username / token identity).</summary>
    public string BuyerId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    public string? UpvestUserId { get; private set; }
    public string? UpvestAccountGroupId { get; private set; }
    public string? UpvestAccountId { get; private set; }

    /// <summary>Change set aside but not yet invested, in euro cents.</summary>
    public long SetAsideCents { get; private set; }

    private readonly List<Investment> _investments = new();
    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    public bool IsAccepted => Status == EnrolmentStatus.Active;

    public void SetUpvestUserId(string id) => UpvestUserId = id;
    public void SetUpvestAccountGroupId(string id) => UpvestAccountGroupId = id;
    public void SetUpvestAccountId(string id) => UpvestAccountId = id;

    public void MarkActive() => Status = EnrolmentStatus.Active;
    public void MarkRejected() => Status = EnrolmentStatus.Rejected;

    /// <summary>
    /// Sets aside the round-up (difference to the next whole euro) for a paid order.
    /// Returns the amount set aside in cents (0 when not an accepted investor or the
    /// total is already a whole number of euros).
    /// </summary>
    public long SetAsideRoundUp(long orderTotalCents)
    {
        if (!IsAccepted) return 0;
        if (orderTotalCents <= 0) return 0;

        var remainder = orderTotalCents % 100;
        var roundUp = remainder == 0 ? 0 : 100 - remainder;
        SetAsideCents += roundUp;
        return roundUp;
    }

    /// <summary>
    /// If the set-aside balance has reached the threshold, moves the whole balance into a new
    /// pending investment and resets the balance to zero. Returns the created investment, or null.
    /// </summary>
    public Investment? TryBeginInvestment()
    {
        if (SetAsideCents < InvestmentThresholdCents) return null;

        var amount = SetAsideCents;
        SetAsideCents = 0;
        var investment = new Investment(amount);
        _investments.Add(investment);
        return investment;
    }

    /// <summary>Returns a failed investment's amount to the set-aside balance so it is not lost.</summary>
    public void RefundFailedInvestment(Investment investment)
    {
        if (investment.Status != InvestmentStatus.Failed) return;
        SetAsideCents += investment.AmountCents;
    }

    /// <summary>Total successfully invested so far, in cents.</summary>
    public long InvestedCents => _investments
        .Where(i => i.Status == InvestmentStatus.Settled)
        .Sum(i => i.AmountCents);
}
