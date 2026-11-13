using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper's enrolment as an investor at Upvest. Keyed to the signed-in shopper by
/// <see cref="ShopperUserName"/>; one record per shopper. Holds the Upvest identifiers the
/// managed-portfolio flows act through and the idempotency keys used to reconcile the sign-up
/// chain after a dropped connection. No personal detail is persisted beyond what a flow needs.
/// </summary>
public class InvestorRecord : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private InvestorRecord() { }
#pragma warning restore CS8618

    public InvestorRecord(string shopperUserName)
    {
        Guard.Against.NullOrEmpty(shopperUserName, nameof(shopperUserName));
        ShopperUserName = shopperUserName;
        Status = PortfolioStatuses.Investor.Pending;
        CreatedAt = DateTimeOffset.UtcNow;

        // Idempotency keys for the sign-up chain are claimed once, up front, so a reconciling
        // retry re-sends the same value rather than creating a duplicate at Upvest.
        UserIdempotencyKey = Guid.NewGuid();
        TaxIdempotencyKey = Guid.NewGuid();
        AccountGroupIdempotencyKey = Guid.NewGuid();
        AccountIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>The signed-in shopper this investor belongs to (JWT name claim). Unique.</summary>
    public string ShopperUserName { get; private set; }

    /// <summary>The Upvest user id — the caller-facing <c>investorId</c>. Null until the user is created.</summary>
    public Guid? UpvestUserId { get; private set; }

    public Guid? AccountGroupId { get; private set; }

    public Guid? AccountId { get; private set; }

    public bool TaxResidencySet { get; private set; }

    public bool IdentifierSet { get; private set; }

    /// <summary>pending / active / rejected — reflects the state at Upvest.</summary>
    public string Status { get; private set; }

    /// <summary>The app model id this investor's portfolio follows, if one has been chosen.</summary>
    public string? SelectedModelId { get; private set; }

    public Guid ConfigurationIdempotencyKey { get; private set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid UserIdempotencyKey { get; private set; }
    public Guid TaxIdempotencyKey { get; private set; }
    public Guid AccountGroupIdempotencyKey { get; private set; }
    public Guid AccountIdempotencyKey { get; private set; }

    public void SetUpvestUser(Guid upvestUserId) => UpvestUserId = upvestUserId;

    public void MarkTaxResidencySet() => TaxResidencySet = true;

    public void MarkIdentifierSet() => IdentifierSet = true;

    public void SetAccount(Guid accountGroupId, Guid accountId)
    {
        AccountGroupId = accountGroupId;
        AccountId = accountId;
    }

    public void SetStatus(string status)
    {
        Guard.Against.NullOrEmpty(status, nameof(status));
        Status = status;
    }

    public void SelectModel(string modelId)
    {
        Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        SelectedModelId = modelId;
    }

    /// <summary>True once Upvest has accepted the shopper and an account able to hold a portfolio is ready.</summary>
    public bool IsActive => Status == PortfolioStatuses.Investor.Active;
}
