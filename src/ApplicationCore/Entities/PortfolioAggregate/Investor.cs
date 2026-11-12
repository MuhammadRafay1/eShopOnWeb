using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// The link between a signed-in shopper and the managed-portfolio account Upvest holds on their behalf.
/// Keyed by the shopper's own identity (<see cref="BuyerId"/>) so one shopper can never see another's.
/// This record holds only the references and the last-known lifecycle state needed to talk to Upvest;
/// the authoritative status is always re-read from Upvest when a caller asks for it.
/// </summary>
public class Investor : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Investor() { }
#pragma warning restore CS8618

    public Investor(string buyerId, Guid signupIdempotencyKey, string ownerName)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        SignupIdempotencyKey = signupIdempotencyKey;
        OwnerName = Guard.Against.NullOrEmpty(ownerName, nameof(ownerName));
        LifecycleStatus = InvestorLifecycleStatus.Pending;
    }

    /// <summary>The shopper's own identity, taken from the JWT. Primary key: a second sign-up for the same shopper is refused by the store.</summary>
    public string BuyerId { get; private set; }

    /// <summary>The Upvest user id provisioned for this shopper, once <c>CreateUser</c> has succeeded.</summary>
    public Guid? UpvestUserId { get; private set; }

    /// <summary>The Upvest account-group id (PERSONAL) created for this shopper.</summary>
    public Guid? AccountGroupId { get; private set; }

    /// <summary>The Upvest PORTFOLIO account id that holds the managed portfolio.</summary>
    public Guid? AccountId { get; private set; }

    /// <summary>The account-group owner's display name, used as the counterparty on sandbox money-in. Never logged.</summary>
    public string OwnerName { get; private set; }

    /// <summary>Last-known lifecycle state; the live status is re-read from Upvest on demand.</summary>
    public InvestorLifecycleStatus LifecycleStatus { get; private set; }

    /// <summary>The app model id the investor has chosen to follow (maps to an Upvest allocation).</summary>
    public string? FollowedModelId { get; private set; }

    /// <summary>Whether a portfolio configuration already exists at Upvest for this account (create vs. update on switch).</summary>
    public bool PortfolioConfigurationExists { get; private set; }

    /// <summary>The Upvest virtual bank account id, created once per account group for sandbox money-in.</summary>
    public Guid? VirtualBankAccountId { get; private set; }

    /// <summary>The idempotency key used for the sign-up provisioning, reused on a reconciling retry.</summary>
    public Guid SignupIdempotencyKey { get; private set; }

    public bool AccountReady => AccountId is not null;

    public void RecordUser(Guid upvestUserId) => UpvestUserId = upvestUserId;

    public void RecordAccountGroup(Guid accountGroupId) => AccountGroupId = accountGroupId;

    public void RecordAccount(Guid accountId) => AccountId = accountId;

    public void RecordVirtualBankAccount(Guid virtualBankAccountId) => VirtualBankAccountId = virtualBankAccountId;

    public void SetLifecycleStatus(InvestorLifecycleStatus status) => LifecycleStatus = status;

    public void FollowModel(string modelId, bool configurationExists)
    {
        FollowedModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
        PortfolioConfigurationExists = configurationExists;
    }
}
