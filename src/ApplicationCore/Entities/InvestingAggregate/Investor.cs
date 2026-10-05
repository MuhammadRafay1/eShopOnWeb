using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Holds the link to the shopper's Upvest
/// investor record and account, their set-aside (pending) balance and the total they have invested.
/// One <see cref="Investor"/> belongs to exactly one shopper (<see cref="BuyerId"/>).
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>The amount (in euro cents) that must be set aside before it is invested: €10.00.</summary>
    public const long InvestmentThresholdCents = 1000;

#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string buyerId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        BuyerId = buyerId;
        Status = EnrolmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        GroupIdempotencyKey = Guid.NewGuid();
        AccountIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>The shopper this investor belongs to (the identity from the JWT).</summary>
    public string BuyerId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    /// <summary>The shopper's user id at Upvest.</summary>
    public string? UpvestUserId { get; private set; }

    public string? AccountGroupId { get; private set; }
    public string? AccountId { get; private set; }

    /// <summary>True once the Upvest trading account has become ACTIVE and can hold orders.</summary>
    public bool AccountActive { get; private set; }

    /// <summary>Set aside from paid orders but not yet invested, in euro cents.</summary>
    public long PendingAmountCents { get; private set; }

    /// <summary>Total successfully invested on the shopper's behalf, in euro cents.</summary>
    public long InvestedAmountCents { get; private set; }

    // Stored so a retried account-group/account creation reuses the same key (exactly-once at Upvest).
    public Guid GroupIdempotencyKey { get; private set; }
    public Guid AccountIdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void LinkUpvestUser(string upvestUserId)
    {
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        UpvestUserId = upvestUserId;
    }

    public void MarkActive() => Status = EnrolmentStatus.Active;
    public void MarkRejected() => Status = EnrolmentStatus.Rejected;

    public void LinkAccount(string accountGroupId, string accountId)
    {
        Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        AccountGroupId = accountGroupId;
        AccountId = accountId;
    }

    public void MarkAccountActive() => AccountActive = true;

    /// <summary>Set aside the round-up from a paid order.</summary>
    public void AddSetAside(long cents)
    {
        Guard.Against.Negative(cents, nameof(cents));
        PendingAmountCents += cents;
    }

    /// <summary>True when the shopper is an accepted investor, has an active account, and the set-aside balance has reached the threshold.</summary>
    public bool CanInvest =>
        Status == EnrolmentStatus.Active &&
        AccountId is not null &&
        AccountActive &&
        PendingAmountCents >= InvestmentThresholdCents;

    /// <summary>Move the whole pending balance into an investment and reset it to zero. Returns the amount taken.</summary>
    public long TakePendingForInvestment()
    {
        var amount = PendingAmountCents;
        PendingAmountCents = 0;
        return amount;
    }

    /// <summary>An investment failed at Upvest — return its money to the set-aside balance so it accrues towards the next attempt.</summary>
    public void ReturnFailedInvestment(long cents)
    {
        Guard.Against.Negative(cents, nameof(cents));
        PendingAmountCents += cents;
    }

    /// <summary>An investment settled at Upvest — record it against the total invested.</summary>
    public void RecordInvested(long cents)
    {
        Guard.Against.Negative(cents, nameof(cents));
        InvestedAmountCents += cents;
    }
}
