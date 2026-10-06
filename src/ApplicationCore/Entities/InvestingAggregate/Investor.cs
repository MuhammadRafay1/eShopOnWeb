using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Holds the link to the shopper's Upvest
/// investor/account, their enrolment status, and their set-aside ledger (money rounded up but not yet
/// invested). One investor per shop identity; an investor's data belongs to that shopper alone.
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
        PendingAmountCents = 0;
    }

    /// <summary>The shop identity (authenticated user name) that owns this investor.</summary>
    public string BuyerId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }

    /// <summary>Change set aside but not yet invested, in euro cents.</summary>
    public long PendingAmountCents { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void LinkUpvest(Guid userId, Guid accountGroupId, Guid accountId)
    {
        UpvestUserId = userId;
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
    }

    public void SetStatus(EnrolmentStatus status) => Status = status;

    /// <summary>Set aside additional change (the round-up of a paid order). Negative values are ignored.</summary>
    public void SetAside(long cents)
    {
        if (cents > 0) PendingAmountCents += cents;
    }

    /// <summary>
    /// If the set-aside balance has reached the threshold, withdraw the whole balance for investment and
    /// reset it to zero. Returns the amount withdrawn in cents, or 0 if the threshold is not met.
    /// </summary>
    public long WithdrawForInvestment(long thresholdCents)
    {
        if (PendingAmountCents < thresholdCents) return 0;
        var amount = PendingAmountCents;
        PendingAmountCents = 0;
        return amount;
    }

    /// <summary>Return a failed investment's amount to the set-aside balance so it accrues again.</summary>
    public void Refund(long cents)
    {
        if (cents > 0) PendingAmountCents += cents;
    }
}
