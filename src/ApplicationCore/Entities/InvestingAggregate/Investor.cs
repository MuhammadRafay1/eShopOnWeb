using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change, together with the money they have
/// set aside but not yet invested. One <see cref="Investor"/> per shopper (keyed by <see cref="BuyerId"/>);
/// a shopper's enrolment, ledger and investments belong to that shopper alone.
///
/// No personal detail (name, address, tax id, ...) is ever stored here — those are sent straight to
/// Upvest at enrolment and only the resulting Upvest identifiers are kept, so there is nothing to leak.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string buyerId)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Status = EnrolmentStatus.Pending;
    }

    /// <summary>The shopper's identity, taken from their authentication token.</summary>
    public string BuyerId { get; private set; }

    /// <summary>The Upvest user id once the shopper has been registered as an investor.</summary>
    public string? UpvestUserId { get; private set; }

    /// <summary>The Upvest account group that owns the shopper's trading account.</summary>
    public string? UpvestAccountGroupId { get; private set; }

    /// <summary>The Upvest trading account that holds what is bought for the shopper.</summary>
    public string? UpvestAccountId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    /// <summary>Spare change set aside and not yet invested, in euros.</summary>
    public decimal PendingAmount { get; private set; }

    /// <summary>Link the Upvest user created for this shopper (before acceptance).</summary>
    public void LinkUpvestUser(string upvestUserId) =>
        UpvestUserId = Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));

    /// <summary>Link the holding account group and trading account, provisioned once Upvest accepts the shopper.</summary>
    public void LinkHoldingAccount(string upvestAccountGroupId, string upvestAccountId)
    {
        UpvestAccountGroupId = Guard.Against.NullOrEmpty(upvestAccountGroupId, nameof(upvestAccountGroupId));
        UpvestAccountId = Guard.Against.NullOrEmpty(upvestAccountId, nameof(upvestAccountId));
    }

    public void UpdateStatus(EnrolmentStatus status) => Status = status;

    /// <summary>A shopper can only invest once Upvest has accepted them as an investor.</summary>
    public bool CanInvest => Status == EnrolmentStatus.Active && !string.IsNullOrEmpty(UpvestUserId) && !string.IsNullOrEmpty(UpvestAccountId);

    /// <summary>Set aside the spare change from one paid order.</summary>
    public void SetAside(decimal amount)
    {
        Guard.Against.Negative(amount, nameof(amount));
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Move the whole set-aside balance out to be invested, leaving the balance at zero.</summary>
    public decimal WithdrawPendingForInvestment()
    {
        var amount = PendingAmount;
        PendingAmount = 0m;
        return amount;
    }

    /// <summary>Return money to the set-aside balance when an investment could not be placed or later failed.</summary>
    public void ReturnToPending(decimal amount)
    {
        Guard.Against.Negative(amount, nameof(amount));
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
    }
}
