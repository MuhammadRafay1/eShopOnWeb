using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper who has signed up to hold a managed investment portfolio with Upvest.
/// Keyed by <see cref="BuyerId"/> (the shopper's identity) so one shopper can never see another's
/// investor record. Holds only identifiers and status — never the personal details sent to Upvest.
/// </summary>
public class PortfolioInvestor : IAggregateRoot
{
    // Required by EF Core
#pragma warning disable CS8618
    private PortfolioInvestor() { }
#pragma warning restore CS8618

    public PortfolioInvestor(string buyerId)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        InvestorId = Guid.NewGuid();
        Status = InvestorStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;

        // Idempotency keys for the provisioning writes are generated once and reused on every
        // resend, so a replay at Upvest returns the original result rather than creating a duplicate.
        CreateUserIdempotencyKey = Guid.NewGuid();
        CreateAccountGroupIdempotencyKey = Guid.NewGuid();
        CreateAccountIdempotencyKey = Guid.NewGuid();
        ChooseModelIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>The shopper's identity (JWT name). Primary key.</summary>
    public string BuyerId { get; private set; }

    /// <summary>Stable application-owned id reported to the caller as <c>investorId</c>.</summary>
    public Guid InvestorId { get; private set; }

    public InvestorStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // Identifiers obtained from Upvest as provisioning progresses. Null until the matching step
    // has completed, which lets a later call resume provisioning from where it stopped.
    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }
    public Guid? UpvestKycCheckId { get; private set; }

    /// <summary>The model the investor's portfolio follows, if they have chosen one.</summary>
    public string? SelectedModelId { get; private set; }

    /// <summary>True once Upvest has accepted the chosen model as a portfolio configuration.</summary>
    public bool ModelConfiguredAtUpvest { get; private set; }

    public Guid CreateUserIdempotencyKey { get; private set; }
    public Guid CreateAccountGroupIdempotencyKey { get; private set; }
    public Guid CreateAccountIdempotencyKey { get; private set; }
    public Guid ChooseModelIdempotencyKey { get; private set; }

    public void SetUpvestUserId(Guid userId) => UpvestUserId = userId;
    public void SetUpvestAccountGroupId(Guid groupId) => UpvestAccountGroupId = groupId;
    public void SetUpvestAccountId(Guid accountId) => UpvestAccountId = accountId;
    public void SetUpvestKycCheckId(Guid checkId) => UpvestKycCheckId = checkId;

    public void MarkActive() => Status = InvestorStatus.Active;
    public void MarkRejected() => Status = InvestorStatus.Rejected;
    public void MarkPending() => Status = InvestorStatus.Pending;

    public void ChooseModel(string modelId)
    {
        SelectedModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        ModelConfiguredAtUpvest = false;
    }

    public void MarkModelConfigured() => ModelConfiguredAtUpvest = true;

    /// <summary>True when Upvest has accepted the shopper and their account is ready to invest.</summary>
    public bool IsReadyToInvest => Status == InvestorStatus.Active;

    /// <summary>True once provisioning at Upvest has produced a user and an investable account.</summary>
    public bool IsProvisioned => UpvestUserId.HasValue && UpvestAccountId.HasValue;
}
