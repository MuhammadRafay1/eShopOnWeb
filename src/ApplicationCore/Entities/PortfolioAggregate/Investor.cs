using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// An eShopOnWeb shopper who has (or is in the process of) becoming a managed-portfolio investor at Upvest.
/// <para>
/// The primary key is the shopper's own username (the JWT identity), so each shopper has at most one investor
/// record and a concurrent duplicate sign-up is rejected by the store on insert (not by a read-then-write race).
/// No personal detail from the sign-up form is persisted here — only the opaque Upvest identifiers and the
/// idempotency keys needed to replay the onboarding writes safely.
/// </para>
/// </summary>
public class Investor : IAggregateRoot
{
    private Investor()
    {
        // Required by EF Core.
        ShopperUserName = string.Empty;
    }

    public Investor(string shopperUserName)
    {
        ShopperUserName = !string.IsNullOrWhiteSpace(shopperUserName)
            ? shopperUserName
            : throw new ArgumentException("Shopper user name is required.", nameof(shopperUserName));
        Status = InvestorStatus.Pending;
        UserIdempotencyKey = Guid.NewGuid();
        AccountGroupIdempotencyKey = Guid.NewGuid();
        AccountIdempotencyKey = Guid.NewGuid();
        ConfigurationIdempotencyKey = Guid.NewGuid();
        VirtualBankAccountIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>Natural primary key: the signed-in shopper's username, taken from the JWT.</summary>
    public string ShopperUserName { get; private set; }

    /// <summary>The Upvest user id (the investorId reported to callers). Null until the user is created.</summary>
    public Guid? UpvestUserId { get; private set; }

    public Guid? AccountGroupId { get; private set; }

    public Guid? AccountId { get; private set; }

    public string? VirtualBankAccountIban { get; private set; }

    /// <summary>The app model id the investor's portfolio follows. Null until a model is chosen.</summary>
    public string? ChosenModelId { get; private set; }

    /// <summary>The Upvest allocation id behind <see cref="ChosenModelId"/>.</summary>
    public Guid? ChosenAllocationId { get; private set; }

    /// <summary>Last status derived from Upvest read-back; refreshed on every read.</summary>
    public InvestorStatus Status { get; private set; }

    // Persisted idempotency keys — the real, caller-supplied dedup keys for the onboarding writes.
    // Persisting them means a replay (within a request, or a later resume) reuses the same key so Upvest
    // deduplicates rather than creating a second resource.
    public Guid UserIdempotencyKey { get; private set; }
    public Guid AccountGroupIdempotencyKey { get; private set; }
    public Guid AccountIdempotencyKey { get; private set; }
    public Guid ConfigurationIdempotencyKey { get; private set; }
    public Guid VirtualBankAccountIdempotencyKey { get; private set; }

    public void SetUpvestUserId(Guid userId) => UpvestUserId = userId;

    public void SetAccountGroupId(Guid accountGroupId) => AccountGroupId = accountGroupId;

    public void SetAccountId(Guid accountId) => AccountId = accountId;

    public void SetVirtualBankAccountIban(string iban) => VirtualBankAccountIban = iban;

    public void ChooseModel(string modelId, Guid allocationId)
    {
        ChosenModelId = modelId;
        ChosenAllocationId = allocationId;
    }

    public void SetStatus(InvestorStatus status) => Status = status;

    /// <summary>The onboarding writes have all landed when the user, account group and account ids are known.</summary>
    public bool OnboardingComplete => UpvestUserId.HasValue && AccountGroupId.HasValue && AccountId.HasValue;

    public bool HasChosenModel => ChosenAllocationId.HasValue;
}
