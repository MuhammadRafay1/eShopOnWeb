using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Aggregate root that owns
/// the set-aside balance (the change not yet invested) and the resulting investments.
/// No personal details are stored here: once relayed to Upvest they are not kept.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string shopperId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));

        ShopperId = shopperId;
        EnrolmentId = Guid.NewGuid();
        Status = EnrolmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>The owning shopper's identity (the authenticated user name from the token).</summary>
    public string ShopperId { get; private set; }

    /// <summary>Stable public identifier, surfaced as <c>enrolmentId</c>.</summary>
    public Guid EnrolmentId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    public string? UpvestUserId { get; private set; }
    public string? UpvestAccountGroupId { get; private set; }
    public string? UpvestAccountId { get; private set; }

    /// <summary>Change set aside but not yet invested, in euro cents.</summary>
    public long PendingAmountCents { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Investment> _investments = new();
    public IReadOnlyCollection<Investment> Investments => _investments.AsReadOnly();

    /// <summary>Change set aside and not yet invested.</summary>
    public decimal PendingAmount => PendingAmountCents / 100m;

    /// <summary>Total money that has been invested so far (money that left the set-aside balance and has not been returned by a failed investment).</summary>
    public decimal InvestedAmount =>
        _investments.Where(i => i.Status != InvestmentStatus.Failed).Sum(i => i.AmountCents) / 100m;

    public bool CanInvest => Status == EnrolmentStatus.Active
        && !string.IsNullOrEmpty(UpvestUserId)
        && !string.IsNullOrEmpty(UpvestAccountGroupId)
        && !string.IsNullOrEmpty(UpvestAccountId);

    public void LinkUpvestUser(string upvestUserId)
    {
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        UpvestUserId = upvestUserId;
        Touch();
    }

    public void LinkUpvestAccounts(string accountGroupId, string accountId)
    {
        Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
        Touch();
    }

    public void MarkActive()
    {
        if (Status == EnrolmentStatus.Active) return;
        Status = EnrolmentStatus.Active;
        Touch();
    }

    public void MarkRejected()
    {
        if (Status == EnrolmentStatus.Rejected) return;
        Status = EnrolmentStatus.Rejected;
        Touch();
    }

    /// <summary>Set aside the round-up from a paid order.</summary>
    public void SetAside(long cents)
    {
        if (cents <= 0) return;
        PendingAmountCents += cents;
        Touch();
    }

    /// <summary>
    /// Move the whole set-aside balance into a new investment. The balance returns to zero.
    /// </summary>
    public Investment BeginInvestment()
    {
        if (PendingAmountCents <= 0)
            throw new InvalidOperationException("There is nothing set aside to invest.");

        var investment = new Investment(PendingAmountCents);
        _investments.Add(investment);
        PendingAmountCents = 0;
        Touch();
        return investment;
    }

    /// <summary>Return a failed investment's money to the set-aside balance so it can be retried.</summary>
    public void RefundToPending(Investment investment)
    {
        PendingAmountCents += investment.AmountCents;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
