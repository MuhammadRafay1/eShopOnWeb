using System;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper who has signed up to hold a managed investment portfolio. Links the shop's shopper
/// (by their authenticated user name) to the Upvest entities created on their behalf, and tracks the
/// idempotency keys so sign-up and account provisioning can be safely replayed.
///
/// One investor record belongs to exactly one shopper; a shopper never sees another's record.
/// No personal data is persisted here — only the linkage to Upvest, where the personal data lives.
/// </summary>
public class Investor : BaseEntity, IAggregateRoot
{
    /// <summary>The authenticated shopper this record belongs to (the JWT name claim).</summary>
    public string ShopperUserName { get; private set; } = default!;

    public Guid? UpvestUserId { get; private set; }
    public Guid? UpvestAccountGroupId { get; private set; }
    public Guid? UpvestAccountId { get; private set; }

    public InvestorStatus Status { get; private set; } = InvestorStatus.Pending;

    /// <summary>The app-side model portfolio the investor's portfolio follows, if chosen.</summary>
    public int? SelectedModelPortfolioId { get; private set; }

    /// <summary>
    /// Deterministic, persisted idempotency keys — reused verbatim on every retry so an Upvest-side
    /// replay is deduplicated, and so account provisioning can be re-attempted safely (self-heal).
    /// </summary>
    public Guid UserIdempotencyKey { get; private set; }
    public Guid AccountGroupIdempotencyKey { get; private set; }
    public Guid AccountIdempotencyKey { get; private set; }
    public Guid ModelChoiceIdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Investor() { } // EF

    public Investor(string shopperUserName)
    {
        ShopperUserName = shopperUserName;
        Status = InvestorStatus.Pending;
        UserIdempotencyKey = Guid.NewGuid();
        AccountGroupIdempotencyKey = Guid.NewGuid();
        AccountIdempotencyKey = Guid.NewGuid();
        ModelChoiceIdempotencyKey = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestUser(Guid userId) => UpvestUserId = userId;

    public void SetUpvestAccount(Guid accountGroupId, Guid accountId)
    {
        UpvestAccountGroupId = accountGroupId;
        UpvestAccountId = accountId;
    }

    public void SetAccountGroup(Guid accountGroupId) => UpvestAccountGroupId = accountGroupId;

    public void SetStatus(InvestorStatus status) => Status = status;

    public void ChooseModel(int modelPortfolioId) => SelectedModelPortfolioId = modelPortfolioId;

    /// <summary>True once Upvest has accepted the shopper and their account is ready.</summary>
    public bool CanInvest => Status == InvestorStatus.Active && UpvestAccountId is not null;

    public bool AccountProvisioned => UpvestAccountGroupId is not null && UpvestAccountId is not null;
}
