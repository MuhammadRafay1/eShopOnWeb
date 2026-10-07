using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. The investor owns the running
/// "set aside" balance (change rounded up from paid orders but not yet invested) and the Upvest
/// references needed to invest on the shopper's behalf. One investor per shopper (<see cref="BuyerId"/>).
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
        EnrolmentId = Guid.NewGuid();
        Status = EnrolmentStatus.Pending;
        CreatedDate = DateTimeOffset.UtcNow;
    }

    /// <summary>The shop's identity for this shopper (the authenticated user name). The linking key.</summary>
    public string BuyerId { get; private set; }

    /// <summary>Stable external identifier for this enrolment, surfaced to callers as <c>enrolmentId</c>.</summary>
    public Guid EnrolmentId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }
    public Guid? UpvestKycCheckId { get; private set; }

    /// <summary>Change set aside so far and not yet invested, in euros.</summary>
    public decimal PendingAmount { get; private set; }

    public DateTimeOffset CreatedDate { get; private set; }

    /// <summary>Record the Upvest user and KYC check created when the sign-up form was submitted.</summary>
    public void SetUpvestUser(Guid userId, Guid kycCheckId)
    {
        UpvestUserId = userId;
        UpvestKycCheckId = kycCheckId;
    }

    /// <summary>Record the Upvest account group and trading account provisioned after acceptance.</summary>
    public void SetUpvestAccounts(Guid accountGroupId, Guid accountId)
    {
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
    }

    public void SetStatus(EnrolmentStatus status) => Status = status;

    /// <summary>Set aside the rounded-up change from a paid order.</summary>
    public void SetAside(decimal amount)
    {
        Guard.Against.Negative(amount, nameof(amount));
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Deduct an amount that has been invested from the set-aside balance. Deducting exactly the
    /// invested amount (rather than zeroing) keeps any change set aside after the investment decision.
    /// </summary>
    public void DeductPending(decimal amount)
    {
        Guard.Against.Negative(amount, nameof(amount));
        PendingAmount = decimal.Round(PendingAmount - amount, 2, MidpointRounding.AwayFromZero);
        if (PendingAmount < 0m)
        {
            PendingAmount = 0m;
        }
    }

    /// <summary>True once Upvest has accepted the shopper and the trading account is known.</summary>
    public bool CanInvest =>
        Status == EnrolmentStatus.Active &&
        UpvestUserId.HasValue &&
        UpvestAccountGroupId.HasValue &&
        UpvestAccountId.HasValue;
}
