using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. The aggregate owns the
/// shopper's set-aside ledger balance and the references to their Upvest entities.
/// It intentionally holds no personal data beyond the shop's buyer id &mdash; the
/// sign-up details are passed straight through to Upvest and never persisted here.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>Stable, externally-facing enrolment id.</summary>
    public Guid EnrolmentId { get; private set; } = Guid.NewGuid();

    /// <summary>The eShopOnWeb buyer id (the signed-in user's name/identity).</summary>
    public string BuyerId { get; private set; }

    public InvestorStatus Status { get; private set; } = InvestorStatus.Pending;

    public string? UpvestUserId { get; private set; }
    public string? UpvestAccountGroupId { get; private set; }
    public string? UpvestAccountId { get; private set; }

    /// <summary>Amount set aside from paid orders but not yet invested.</summary>
    public decimal PendingAmount { get; private set; }

    public DateTimeOffset CreatedDate { get; private set; } = DateTimeOffset.UtcNow;

    #pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }

    public Investor(string buyerId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        BuyerId = buyerId;
    }

    public void LinkUpvestUser(string upvestUserId)
    {
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        UpvestUserId = upvestUserId;
    }

    public void LinkUpvestAccounts(string accountGroupId, string accountId)
    {
        Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
    }

    public void Activate() => Status = InvestorStatus.Active;

    public void Reject() => Status = InvestorStatus.Rejected;

    public bool IsAccepted => Status == InvestorStatus.Active;

    /// <summary>Adds the rounded-up change from a paid order to the set-aside balance.</summary>
    public void SetAside(decimal amount)
    {
        Guard.Against.Negative(amount, nameof(amount));
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Returns true once enough has been set aside to invest.</summary>
    public bool HasReachedInvestmentThreshold(decimal threshold) => PendingAmount >= threshold;

    /// <summary>Moves the whole set-aside balance out to be invested, zeroing it.</summary>
    public decimal WithdrawSetAsideForInvestment()
    {
        var amount = PendingAmount;
        PendingAmount = 0m;
        return amount;
    }

    /// <summary>Returns a failed investment's amount to the set-aside balance so it accrues again.</summary>
    public void ReturnFailedAmount(decimal amount)
    {
        Guard.Against.Negative(amount, nameof(amount));
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
    }
}
