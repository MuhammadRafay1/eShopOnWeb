using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The app-side record that links an eShop shopper to the investor, account group and account
/// Upvest holds on their behalf, plus the model portfolio they have chosen. Upvest remains the
/// system of record for money and holdings; this entity holds only correlation ids, the
/// idempotency keys used to make the onboarding writes safe to retry, and a cached status.
/// Personal details are never stored here.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string shopperId)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        ShopperId = shopperId;
        CreatedAt = DateTimeOffset.UtcNow;
        // Stable idempotency keys, generated once with the claim row and reused on every retry /
        // reconciliation of the matching Upvest write, so a resend never creates a second resource.
        UserCreateKey = Guid.NewGuid();
        TaxResidencyKey = Guid.NewGuid();
        AccountGroupCreateKey = Guid.NewGuid();
        AccountCreateKey = Guid.NewGuid();
    }

    /// <summary>The eShop shopper this investor belongs to (JWT identity name). Unique.</summary>
    public string ShopperId { get; private set; }

    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestKycCheckId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }

    /// <summary>The app model id the investor's portfolio follows (operator-defined), if chosen.</summary>
    public string? SelectedModelId { get; private set; }
    /// <summary>The Upvest allocation id behind <see cref="SelectedModelId"/>.</summary>
    public Guid? SelectedAllocationId { get; private set; }

    public Guid UserCreateKey { get; private set; }
    public Guid TaxResidencyKey { get; private set; }
    public Guid AccountGroupCreateKey { get; private set; }
    public Guid AccountCreateKey { get; private set; }

    /// <summary>Last status learned from Upvest ("pending"/"active"/"rejected"); a fallback for reads.</summary>
    public string StatusCache { get; private set; } = "pending";

    public DateTimeOffset CreatedAt { get; private set; }

    public void SetUpvestUser(Guid userId) => UpvestUserId = userId;
    public void SetKycCheck(Guid checkId) => UpvestKycCheckId = checkId;
    public void SetAccountGroup(Guid accountGroupId) => UpvestAccountGroupId = accountGroupId;
    public void SetAccount(Guid accountId) => UpvestAccountId = accountId;

    public void SelectModel(string modelId, Guid allocationId)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        SelectedModelId = modelId;
        SelectedAllocationId = allocationId;
    }

    public void UpdateStatusCache(string status) => StatusCache = status;

    /// <summary>Has Upvest provisioned the account that can hold a managed portfolio?</summary>
    public bool CanInvest => UpvestUserId.HasValue && UpvestAccountGroupId.HasValue && UpvestAccountId.HasValue;
}
