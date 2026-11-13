using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper who has signed up to hold a managed investment portfolio with Upvest.
/// Scoped to a single shopper via <see cref="ShopperId"/> (the authenticated caller's identity);
/// one shopper has at most one investor record and may never see another's.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string shopperId, string upvestUserId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        ShopperId = shopperId;
        UpvestUserId = upvestUserId;
        Status = InvestorStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Identity of the shopper this investor record belongs to (the JWT subject / username).</summary>
    public string ShopperId { get; private set; }

    /// <summary>The user id assigned by Upvest. This is the investor identity reported to callers.</summary>
    public string UpvestUserId { get; private set; }

    /// <summary>The Upvest account group that holds the shopper's cash balance, once created.</summary>
    public string? UpvestAccountGroupId { get; private set; }

    /// <summary>The Upvest (PORTFOLIO) account that holds the managed portfolio, once created.</summary>
    public string? UpvestAccountId { get; private set; }

    /// <summary>The model portfolio the investor has chosen to follow (business key of a <see cref="ModelPortfolio"/>).</summary>
    public string? SelectedModelId { get; private set; }

    public InvestorStatus Status { get; private set; }

    /// <summary>Human-readable, non-personal note about the current status (e.g. which onboarding step is outstanding).</summary>
    public string? StatusDetail { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void LinkAccountGroup(string accountGroupId)
    {
        Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        UpvestAccountGroupId = accountGroupId;
    }

    public void LinkAccount(string accountId)
    {
        Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        UpvestAccountId = accountId;
    }

    public void SelectModel(string modelId)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        SelectedModelId = modelId;
    }

    public void SetStatus(InvestorStatus status, string? detail = null)
    {
        Status = status;
        StatusDetail = detail;
    }
}
