using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper who has signed up to hold a managed investment portfolio with Upvest.
/// Holds only the minimum needed to talk to Upvest on the shopper's behalf; no personal
/// details are persisted here (they are sent to Upvest and never retained or logged).
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>Stable, externally exposed identifier (returned as <c>investorId</c>).</summary>
    public Guid PublicId { get; private set; } = Guid.NewGuid();

    /// <summary>
    /// The owning shopper's identity (the authenticated user name / email from the JWT).
    /// One shopper owns exactly one investor record; it is never shared.
    /// </summary>
    public string ShopperId { get; private set; }

    /// <summary>The Upvest user id once the user has been created.</summary>
    public string? UpvestUserId { get; private set; }

    /// <summary>The Upvest account group id once created.</summary>
    public string? UpvestAccountGroupId { get; private set; }

    /// <summary>The Upvest account that holds the managed portfolio, once created.</summary>
    public string? UpvestAccountId { get; private set; }

    public InvestorStatus Status { get; private set; } = InvestorStatus.Pending;

    /// <summary>The model portfolio this investor follows, if one has been chosen.</summary>
    public int? ModelId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public Investor(string shopperId)
    {
        ShopperId = Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
    }

#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public void SetUpvestUser(string upvestUserId)
    {
        UpvestUserId = Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));
        Touch();
    }

    public void SetUpvestAccount(string accountGroupId, string accountId)
    {
        UpvestAccountGroupId = Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        UpvestAccountId = Guard.Against.NullOrEmpty(accountId, nameof(accountId));
        Touch();
    }

    public void SetStatus(InvestorStatus status)
    {
        Status = status;
        Touch();
    }

    public void ChooseModel(int modelId)
    {
        ModelId = modelId;
        Touch();
    }

    /// <summary>True once the investor can pay money in: accepted by Upvest with a ready account and a chosen model.</summary>
    public bool CanPayIn => Status == InvestorStatus.Active
        && !string.IsNullOrEmpty(UpvestUserId)
        && !string.IsNullOrEmpty(UpvestAccountId)
        && ModelId.HasValue;

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
