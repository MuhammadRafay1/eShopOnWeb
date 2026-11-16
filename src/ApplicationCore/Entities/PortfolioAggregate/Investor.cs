using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Application-owned record that ties one eShop shopper (<see cref="BuyerId"/>) to the resources held
/// on their behalf at Upvest. Personal details are NOT stored here — only the opaque Upvest identifiers
/// needed to drive and read back the investor's managed portfolio. One investor per shopper.
/// </summary>
public class Investor
{
    // Parameterless ctor kept for serialization/testing scenarios.
    private Investor() { BuyerId = string.Empty; }

    public Investor(string buyerId)
    {
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            throw new ArgumentException("buyerId is required", nameof(buyerId));
        }

        BuyerId = buyerId;
        CreatedAt = DateTimeOffset.UtcNow;

        // Real, caller-supplied idempotency keys for each onboarding write (persisted before the call so a
        // connection failure after the provider acted can be reconciled by replaying the same key).
        UserIdempotencyKey = Guid.NewGuid();
        TaxIdempotencyKey = Guid.NewGuid();
        AccountGroupIdempotencyKey = Guid.NewGuid();
        AccountIdempotencyKey = Guid.NewGuid();
        VirtualBankAccountIdempotencyKey = Guid.NewGuid();
    }

    /// <summary>The owning shopper — the JWT identity name. The store's primary key; one investor per shopper.</summary>
    public string BuyerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    // Upvest resource identifiers, filled in as the onboarding chain progresses.
    public Guid? UpvestUserId { get; set; }
    public Guid? CheckId { get; set; }
    public Guid? AccountGroupId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? VirtualBankAccountId { get; set; }

    /// <summary>The operator model (by its route modelId) this investor's portfolio currently follows.</summary>
    public string? SelectedModelId { get; set; }

    // Persisted idempotency keys (see constructor).
    public Guid UserIdempotencyKey { get; private set; }
    public Guid TaxIdempotencyKey { get; private set; }
    public Guid AccountGroupIdempotencyKey { get; private set; }
    public Guid AccountIdempotencyKey { get; private set; }
    public Guid VirtualBankAccountIdempotencyKey { get; private set; }

    /// <summary>True once the full onboarding chain reached account creation at Upvest.</summary>
    public bool OnboardingComplete => UpvestUserId.HasValue && AccountGroupId.HasValue && AccountId.HasValue;
}
