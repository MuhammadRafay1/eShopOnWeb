using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Links a signed-in shopper to their investor identity and account at Upvest, and records which
/// model portfolio they have chosen to follow. Personal details are NOT stored here – they are
/// sent straight to Upvest, which is the system of record for them.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    public string ShopperUserName { get; private set; }
    public string? UpvestUserId { get; private set; }
    public string? UpvestAccountGroupId { get; private set; }
    public string? UpvestAccountId { get; private set; }
    public string? SelectedModelId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Investor()
    {
        // EF Core
        ShopperUserName = string.Empty;
    }

    public Investor(string shopperUserName)
    {
        ShopperUserName = Guard.Against.NullOrEmpty(shopperUserName, nameof(shopperUserName));
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestUser(string upvestUserId)
    {
        UpvestUserId = Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
    }

    public void SetUpvestAccount(string accountGroupId, string accountId)
    {
        UpvestAccountGroupId = Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        UpvestAccountId = Guard.Against.NullOrEmpty(accountId, nameof(accountId));
    }

    public void SelectModel(string modelId)
    {
        SelectedModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
    }
}
