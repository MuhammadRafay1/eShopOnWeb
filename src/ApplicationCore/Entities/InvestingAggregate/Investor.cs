using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change through Upvest.
/// Holds only the Upvest identifiers and the running set-aside ledger — never the
/// shopper's personal sign-up details, which are passed straight to Upvest and not retained.
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
    }

    /// <summary>The shopper this enrolment belongs to (their identity from the JWT).</summary>
    public string BuyerId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    public string? UpvestUserId { get; private set; }
    public string? UpvestAccountGroupId { get; private set; }
    public string? UpvestAccountId { get; private set; }

    /// <summary>Change set aside and not yet invested, in euros.</summary>
    public decimal PendingAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void LinkUpvestUser(string upvestUserId)
    {
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        UpvestUserId = upvestUserId;
    }

    public void MarkActive() => Status = EnrolmentStatus.Active;

    public void MarkRejected() => Status = EnrolmentStatus.Rejected;

    public void LinkUpvestAccount(string accountGroupId, string accountId)
    {
        Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
    }

    public bool IsAcceptedInvestor => Status == EnrolmentStatus.Active;

    public bool HasUpvestAccount => !string.IsNullOrEmpty(UpvestAccountGroupId) && !string.IsNullOrEmpty(UpvestAccountId);

    /// <summary>Sets aside the given amount of change (must be positive).</summary>
    public void SetAsideChange(decimal amount)
    {
        if (amount <= 0m) return;
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Clears the set-aside balance once it has been committed to an investment.</summary>
    public void ClearPending() => PendingAmount = 0m;

    /// <summary>Returns a committed amount to the set-aside balance (e.g. when an investment fails).</summary>
    public void ReturnToPending(decimal amount)
    {
        if (amount <= 0m) return;
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
    }
}
