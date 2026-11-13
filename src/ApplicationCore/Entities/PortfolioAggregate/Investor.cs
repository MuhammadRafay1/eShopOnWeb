using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Links an eShop shopper (identified by their account user name) to the investor
/// resources created on their behalf at Upvest. Holds only the Upvest identifiers and
/// the shopper's chosen model portfolio — never any personal data, which is sent to
/// Upvest but never persisted or logged by the shop.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string buyerId, string upvestUserId)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        BuyerId = buyerId;
        UpvestUserId = upvestUserId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The owning shopper's identity (the JWT subject / user name). Scopes all access.</summary>
    public string BuyerId { get; private set; }

    /// <summary>The Upvest user id. This is surfaced to the caller as <c>investorId</c>.</summary>
    public string UpvestUserId { get; private set; }

    /// <summary>The Upvest account group that owns the cash balance.</summary>
    public string? UpvestAccountGroupId { get; private set; }

    /// <summary>The Upvest (PORTFOLIO) account that holds the managed portfolio.</summary>
    public string? UpvestAccountId { get; private set; }

    /// <summary>The model portfolio the investor currently follows, if any.</summary>
    public string? SelectedModelId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetAccountGroup(string accountGroupId)
    {
        Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        UpvestAccountGroupId = accountGroupId;
    }

    public void SetAccount(string accountId)
    {
        Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        UpvestAccountId = accountId;
    }

    public void SelectModel(string modelId)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        SelectedModelId = modelId;
    }
}
