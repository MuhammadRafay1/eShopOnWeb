using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Links a signed-in shop user ("buyer") to the user / account group / account that Upvest holds
/// their managed investment portfolio in. Only non-personal linkage data is persisted here; all
/// personal details are sent to Upvest and never stored (or logged) by the shop.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string buyerId, Guid upvestUserId)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        UpvestUserId = Guard.Against.Default(upvestUserId, nameof(upvestUserId));
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The shop identity (JWT name) of the shopper who owns this investor record.</summary>
    public string BuyerId { get; private set; }

    public Guid UpvestUserId { get; private set; }

    public Guid? UpvestAccountGroupId { get; private set; }

    public Guid? UpvestAccountId { get; private set; }

    /// <summary>External id (from PUT /models/{modelId}) of the model this investor's portfolio follows.</summary>
    public string? SelectedModelId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetAccountGroup(Guid accountGroupId) =>
        UpvestAccountGroupId = Guard.Against.Default(accountGroupId, nameof(accountGroupId));

    public void SetAccount(Guid accountId) =>
        UpvestAccountId = Guard.Against.Default(accountId, nameof(accountId));

    public void SelectModel(string modelId) =>
        SelectedModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
}
