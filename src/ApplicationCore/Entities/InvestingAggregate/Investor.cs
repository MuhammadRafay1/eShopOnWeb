using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Owns their enrolment with Upvest,
/// their set-aside balance (change not yet invested) and the investments made on their behalf.
/// Belongs to exactly one shopper (<see cref="ShopperId"/>); no shopper ever sees another's.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>The whole balance is invested once it reaches this many euro-cents (€10).</summary>
    public const long InvestmentThresholdInCents = 1000;

    #pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }

    public Investor(string shopperId, Guid upvestUserId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.Default(upvestUserId, nameof(upvestUserId));

        ShopperId = shopperId;
        UpvestUserId = upvestUserId;
        Status = InvestorStatus.Pending;
        EnrolledDate = DateTimeOffset.UtcNow;
    }

    /// <summary>Identity of the owning shopper (the authenticated user name from the token).</summary>
    public string ShopperId { get; private set; }

    public Guid UpvestUserId { get; private set; }

    /// <summary>The Upvest account that holds the shopper's investments; provisioned after acceptance.</summary>
    public Guid? UpvestAccountId { get; private set; }

    public InvestorStatus Status { get; private set; }

    /// <summary>Change set aside and not yet invested, in euro-cents.</summary>
    public long PendingAmountInCents { get; private set; }

    public DateTimeOffset EnrolledDate { get; private set; }

    private readonly List<Investment> _investments = new();
    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    /// <summary>Total committed to investing (placed or settled), in euro-cents. Failed tranches are excluded.</summary>
    public long InvestedAmountInCents =>
        _investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.AmountInCents);

    public bool IsAccepted => Status == InvestorStatus.Active;

    public void MarkAccepted() => Status = InvestorStatus.Active;

    public void MarkRejected() => Status = InvestorStatus.Rejected;

    public void SetInvestmentAccount(Guid upvestAccountId)
    {
        Guard.Against.Default(upvestAccountId, nameof(upvestAccountId));
        UpvestAccountId = upvestAccountId;
    }

    /// <summary>
    /// Set aside the round-up from a paid order. Returns true when the balance has reached the
    /// investment threshold and a tranche should now be invested.
    /// </summary>
    public bool SetAsideChange(long roundUpInCents)
    {
        Guard.Against.Negative(roundUpInCents, nameof(roundUpInCents));
        PendingAmountInCents += roundUpInCents;
        return PendingAmountInCents >= InvestmentThresholdInCents;
    }

    /// <summary>
    /// Move the whole set-aside balance into a new pending investment and reset the balance to zero.
    /// The returned tranche is not yet placed at Upvest.
    /// </summary>
    public Investment BeginInvestment()
    {
        if (PendingAmountInCents <= 0)
            throw new InvalidOperationException("There is nothing set aside to invest.");

        var tranche = new Investment(PendingAmountInCents, Guid.NewGuid());
        _investments.Add(tranche);
        PendingAmountInCents = 0;
        return tranche;
    }

    /// <summary>
    /// Abandon a tranche whose placement was definitively rejected, returning its amount to the
    /// set-aside balance so it accrues towards the next attempt.
    /// </summary>
    public void FailInvestment(Investment tranche)
    {
        Guard.Against.Null(tranche, nameof(tranche));
        if (tranche.Status == InvestmentStatus.Failed) return;
        tranche.MarkFailed();
        PendingAmountInCents += tranche.AmountInCents;
    }
}
