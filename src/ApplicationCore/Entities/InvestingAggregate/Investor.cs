using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. The aggregate owns the shopper's
/// round-up ledger (the set-aside balance), the investments made on their behalf, and the link to
/// the matching investor held at Upvest.
///
/// No personal data is stored here: the sign-up details are passed straight to Upvest at enrolment
/// and never persisted, so an investor cannot leak another shopper's personal information.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>The set-aside balance at which the whole balance is invested (euros).</summary>
    public const decimal InvestmentThreshold = 10m;

    #pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
    #pragma warning restore CS8618

    public Investor(string shopperId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));

        ShopperId = shopperId;
        EnrolmentId = Guid.NewGuid();
        Status = EnrolmentStatus.Pending;
        PendingAmount = 0m;
    }

    /// <summary>
    /// Identity of the shopper that owns this investor, taken from the authenticated token.
    /// This is the partition key that keeps one shopper's data out of another's reach.
    /// </summary>
    public string ShopperId { get; private set; }

    /// <summary>Stable identifier handed back to callers as <c>enrolmentId</c>.</summary>
    public Guid EnrolmentId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    /// <summary>Euros set aside from round-ups that have not yet been invested.</summary>
    public decimal PendingAmount { get; private set; }

    // Upvest linkage, captured when the enrolment is submitted. Not personal data.
    public string? UpvestUserId { get; private set; }
    public string? UpvestAccountGroupId { get; private set; }
    public string? UpvestAccountId { get; private set; }

    private readonly List<Investment> _investments = new();
    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    /// <summary>Total euros currently invested on the shopper's behalf (pending plus settled orders).</summary>
    public decimal InvestedAmount =>
        _investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.Amount);

    /// <summary>True once Upvest has accepted the shopper and they are able to hold investments.</summary>
    public bool CanInvest => Status == EnrolmentStatus.Active;

    /// <summary>True once the Upvest account the investments are held in has been provisioned.</summary>
    public bool HasAccount => !string.IsNullOrEmpty(UpvestAccountId);

    public void SetUpvestUser(string upvestUserId) => UpvestUserId = upvestUserId;

    public void SetUpvestAccount(string upvestAccountGroupId, string upvestAccountId)
    {
        UpvestAccountGroupId = upvestAccountGroupId;
        UpvestAccountId = upvestAccountId;
    }

    public void MarkActive()
    {
        if (Status == EnrolmentStatus.Rejected) return; // a rejected investor does not become active
        Status = EnrolmentStatus.Active;
    }

    public void MarkRejected() => Status = EnrolmentStatus.Rejected;

    /// <summary>
    /// Sets aside the round-up from a paid order. Returns the amount set aside (zero when the
    /// shopper is not an accepted investor, or when there is nothing to round up).
    /// </summary>
    public decimal SetAsideRoundUp(decimal orderTotal)
    {
        if (!CanInvest) return 0m;

        var roundUp = RoundUpFor(orderTotal);
        if (roundUp <= 0m) return 0m;

        PendingAmount += roundUp;
        return roundUp;
    }

    /// <summary>
    /// If the set-aside balance has reached the threshold, moves the whole balance into a new,
    /// pending investment and returns it; otherwise returns null. The balance resets to zero so
    /// later round-ups accrue towards the next investment.
    /// </summary>
    public Investment? StartInvestmentIfThresholdReached()
    {
        if (PendingAmount < InvestmentThreshold) return null;

        var investment = new Investment(PendingAmount);
        PendingAmount = 0m;
        _investments.Add(investment);
        return investment;
    }

    public void SettleInvestment(Investment investment) => investment.MarkSettled();

    /// <summary>
    /// A failed investment returns its amount to the set-aside balance, so the shopper's money is
    /// not lost and accrues towards the next attempt.
    /// </summary>
    public void FailInvestment(Investment investment)
    {
        if (investment.Status == InvestmentStatus.Failed) return;
        investment.MarkFailed();
        PendingAmount += investment.Amount;
    }

    /// <summary>The difference between an order total and the next whole euro.</summary>
    public static decimal RoundUpFor(decimal orderTotal)
    {
        var roundUp = Math.Ceiling(orderTotal) - orderTotal;
        return decimal.Round(roundUp, 2, MidpointRounding.AwayFromZero);
    }
}
