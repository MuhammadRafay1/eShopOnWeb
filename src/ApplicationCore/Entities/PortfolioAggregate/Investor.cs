using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper who has signed up to hold a managed investment portfolio with Upvest.
/// One investor record belongs to exactly one shopper (identified by <see cref="ShopperId"/>).
/// No personal data is persisted here — it is sent to Upvest at sign-up and only the
/// resulting Upvest identifiers are retained.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>The signed-in shopper this investor belongs to (the identity from the JWT).</summary>
    public string ShopperId { get; private set; }

    /// <summary>The Upvest user id, once the user has been created at Upvest.</summary>
    public string? UpvestUserId { get; private set; }

    /// <summary>The Upvest account group id, once created.</summary>
    public string? UpvestAccountGroupId { get; private set; }

    /// <summary>The Upvest (portfolio) account id able to hold the managed portfolio, once created.</summary>
    public string? UpvestAccountId { get; private set; }

    /// <summary>The model portfolio the investor currently follows, if chosen.</summary>
    public int? SelectedModelId { get; private set; }

    /// <summary>Last status observed from Upvest. The source of truth remains Upvest.</summary>
    public InvestorStatus Status { get; private set; } = InvestorStatus.Pending;

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string shopperId)
    {
        ShopperId = Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
    }

    public void SetUpvestUser(string upvestUserId)
        => UpvestUserId = Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));

    public void SetUpvestAccountGroup(string accountGroupId)
        => UpvestAccountGroupId = Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));

    public void SetUpvestAccount(string accountId)
        => UpvestAccountId = Guard.Against.NullOrEmpty(accountId, nameof(accountId));

    public void SelectModel(int modelId) => SelectedModelId = modelId;

    public void UpdateStatus(InvestorStatus status) => Status = status;
}
