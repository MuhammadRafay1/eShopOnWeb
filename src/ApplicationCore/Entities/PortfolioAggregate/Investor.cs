using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper who has signed up to hold a managed investment portfolio with Upvest.
/// <para>
/// This record maps a shop shopper (identified by <see cref="ShopperId"/>, which is the
/// authenticated caller's identity) to the entities Upvest holds on their behalf. It deliberately
/// stores no personal data: the sign-up details are forwarded to Upvest and never persisted here.
/// </para>
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>Identity of the shop shopper this investor belongs to (the JWT subject/name).</summary>
    public string ShopperId { get; private set; }

    /// <summary>Identifier Upvest assigned to the underlying user.</summary>
    public string? UpvestUserId { get; private set; }

    /// <summary>Identifier of the Upvest account group that owns the portfolio account.</summary>
    public string? UpvestAccountGroupId { get; private set; }

    /// <summary>Identifier of the Upvest account able to hold the managed portfolio.</summary>
    public string? UpvestAccountId { get; private set; }

    /// <summary>Identifier of the KYC check registered for this investor at Upvest, if any.</summary>
    public string? UpvestKycCheckId { get; private set; }

    /// <summary>Last status learned from Upvest. The live status is refreshed from Upvest on read.</summary>
    public InvestorStatus Status { get; private set; }

    /// <summary>The operator-defined model portfolio this investor's portfolio follows, if chosen.</summary>
    public string? ModelId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string shopperId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        ShopperId = shopperId;
        Status = InvestorStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestUser(string upvestUserId)
    {
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        UpvestUserId = upvestUserId;
    }

    public void SetUpvestAccount(string accountGroupId, string accountId)
    {
        Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
    }

    public void SetUpvestKycCheck(string checkId)
    {
        Guard.Against.NullOrEmpty(checkId, nameof(checkId));
        UpvestKycCheckId = checkId;
    }

    public void UpdateStatus(InvestorStatus status) => Status = status;

    public void ChooseModel(string modelId)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        ModelId = modelId;
    }
}
