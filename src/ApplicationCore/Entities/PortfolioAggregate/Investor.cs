using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Links a signed-in shopper to the investor, account group and managed-portfolio account that
/// represent them at Upvest. Holds only the minimum needed to correlate the shopper with Upvest
/// and to fund their account; it is never written to logs.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string shopperId, string firstName, string lastName)
    {
        ShopperId = Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        FirstName = Guard.Against.NullOrEmpty(firstName, nameof(firstName));
        LastName = Guard.Against.NullOrEmpty(lastName, nameof(lastName));
        Status = InvestorStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The storefront user name (from the JWT) this investor record belongs to.</summary>
    public string ShopperId { get; private set; }

    public string FirstName { get; private set; }
    public string LastName { get; private set; }

    /// <summary>The Upvest user id (UUID) once the shopper has been created at Upvest.</summary>
    public string? UpvestUserId { get; private set; }

    /// <summary>The Upvest account group id (UUID).</summary>
    public string? UpvestAccountGroupId { get; private set; }

    /// <summary>The 9-digit securities account number used to assign funding to the account group.</summary>
    public string? SecuritiesAccountNumber { get; private set; }

    /// <summary>The Upvest portfolio account id (UUID) that holds the managed portfolio.</summary>
    public string? UpvestAccountId { get; private set; }

    /// <summary>The model portfolio this investor currently follows, if any.</summary>
    public string? FollowedModelId { get; private set; }

    /// <summary>The Upvest allocation id the investor's current configuration points at.</summary>
    public string? FollowedAllocationId { get; private set; }

    /// <summary>Last status observed at Upvest.</summary>
    public InvestorStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public void LinkUpvestUser(string upvestUserId)
        => UpvestUserId = Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));

    public void LinkAccountGroup(string accountGroupId, string? securitiesAccountNumber)
    {
        UpvestAccountGroupId = Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));
        SecuritiesAccountNumber = securitiesAccountNumber;
    }

    public void LinkAccount(string accountId)
        => UpvestAccountId = Guard.Against.NullOrEmpty(accountId, nameof(accountId));

    public void FollowModel(string modelId, string? allocationId)
    {
        FollowedModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        FollowedAllocationId = allocationId;
    }

    public void SetStatus(InvestorStatus status) => Status = status;

    public bool CanPayIn => Status == InvestorStatus.Active
        && !string.IsNullOrEmpty(UpvestAccountId)
        && !string.IsNullOrEmpty(FollowedModelId);
}
