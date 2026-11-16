using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The shop-side record that maps a signed-in shopper to the user, account group and account
/// Upvest holds on their behalf. One row per shopper (<see cref="ShopUserId"/> is unique).
/// This record carries no personal data — those are sent straight to Upvest and never persisted.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string shopUserId)
    {
        ShopUserId = Guard.Against.NullOrEmpty(shopUserId, nameof(shopUserId));
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The shopper's identity taken from the JWT (unique). Scopes every read/write to its owner.</summary>
    public string ShopUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The Upvest user id once the user has been created.</summary>
    public Guid? UpvestUserId { get; private set; }

    /// <summary>The Upvest account group (PERSONAL) id once created.</summary>
    public Guid? UpvestAccountGroupId { get; private set; }

    /// <summary>The Upvest PORTFOLIO account id once created — the account that holds the managed portfolio.</summary>
    public Guid? UpvestAccountId { get; private set; }

    /// <summary>The shop model id the investor's portfolio currently follows (null until chosen).</summary>
    public string? SelectedModelId { get; private set; }

    public void SetUpvestUser(Guid upvestUserId) => UpvestUserId = upvestUserId;

    public void SetAccountGroup(Guid accountGroupId) => UpvestAccountGroupId = accountGroupId;

    public void SetAccount(Guid accountId) => UpvestAccountId = accountId;

    public void SelectModel(string modelId) =>
        SelectedModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));

    /// <summary>True once Upvest has been given enough to hold a managed portfolio.</summary>
    public bool HasAccount => UpvestAccountId.HasValue;
}
