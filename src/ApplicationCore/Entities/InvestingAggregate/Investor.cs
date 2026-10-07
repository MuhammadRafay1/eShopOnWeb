using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Holds the link to the
/// Upvest investor/account created on their behalf and the running spare-change balance.
///
/// Money is kept as whole euro cents (long) to avoid any rounding drift; the public API
/// converts to/from euro decimals at the edge. No personal detail is stored here — only
/// the shopper's own identity (<see cref="BuyerId"/>), the Upvest identifiers, and amounts.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string buyerId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        BuyerId = buyerId;
        Status = EnrolmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>The shopper this enrolment belongs to (their authenticated identity).</summary>
    public string BuyerId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }

    /// <summary>Spare change set aside and not yet invested, in euro cents.</summary>
    public long PendingAmountCents { get; private set; }

    /// <summary>Total invested on the shopper's behalf so far (excludes failed), in euro cents.</summary>
    public long InvestedAmountCents { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The whole balance is invested once it reaches this threshold (€10).</summary>
    public const long InvestmentThresholdCents = 1000;

    public void LinkUpvestInvestor(Guid userId, Guid accountGroupId, Guid accountId)
    {
        UpvestUserId = userId;
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

    public void MarkPending()
    {
        Status = EnrolmentStatus.Pending;
        Touch();
    }

    /// <summary>Only an accepted investor may set change aside.</summary>
    public bool IsAccepted => Status == EnrolmentStatus.Active;

    /// <summary>Set spare change aside towards the next investment. Caller guards acceptance.</summary>
    public void SetAside(long cents)
    {
        Guard.Against.Negative(cents, nameof(cents));
        if (cents == 0) return;
        PendingAmountCents += cents;
        Touch();
    }

    public bool ReadyToInvest => IsAccepted && PendingAmountCents >= InvestmentThresholdCents;

    /// <summary>
    /// Takes the whole set-aside balance to invest it, moving it from pending to invested.
    /// Returns the amount withdrawn (in cents).
    /// </summary>
    public long WithdrawPendingForInvestment()
    {
        var amount = PendingAmountCents;
        PendingAmountCents = 0;
        InvestedAmountCents += amount;
        Touch();
        return amount;
    }

    /// <summary>An investment failed at Upvest: return its amount to the set-aside balance.</summary>
    public void ReturnFailedInvestment(long cents)
    {
        Guard.Against.Negative(cents, nameof(cents));
        InvestedAmountCents -= cents;
        if (InvestedAmountCents < 0) InvestedAmountCents = 0;
        PendingAmountCents += cents;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
