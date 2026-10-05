using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper who has opted in to investing their spare change. Holds only what the shop needs to keep:
/// the provider-side identifiers, the enrolment status, and the running set-aside balance. Personal
/// sign-up details are passed to the provider at enrolment time and deliberately never persisted here.
/// </summary>
public class Enrolment : BaseEntity, IAggregateRoot
{
    private Enrolment() { } // EF

    public Enrolment(string shopperId, Guid createUserIdempotencyKey)
    {
        Guard.Against.NullOrEmpty(shopperId, nameof(shopperId));
        ShopperId = shopperId;
        CreateUserIdempotencyKey = createUserIdempotencyKey;
        TaxIdempotencyKey = Guid.NewGuid();
        AccountGroupIdempotencyKey = Guid.NewGuid();
        AccountIdempotencyKey = Guid.NewGuid();
        Status = EnrolmentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    /// <summary>The shop's identity for the shopper (the JWT name claim). One enrolment per shopper.</summary>
    public string ShopperId { get; private set; } = default!;

    public EnrolmentStatus Status { get; private set; }

    /// <summary>The provider's investor id, once the shopper has been created there.</summary>
    public Guid? UpvestUserId { get; private set; }

    /// <summary>The provider account group that holds the shopper's cash, once provisioned.</summary>
    public Guid? AccountGroupId { get; private set; }

    /// <summary>The provider trading account the shopper's orders are placed on, once provisioned.</summary>
    public Guid? AccountId { get; private set; }

    /// <summary>Stable idempotency keys for the provider calls, so a replay cannot duplicate.</summary>
    public Guid CreateUserIdempotencyKey { get; private set; }
    public Guid TaxIdempotencyKey { get; private set; }
    public Guid AccountGroupIdempotencyKey { get; private set; }
    public Guid AccountIdempotencyKey { get; private set; }

    /// <summary>Whether the KYC check and tax residency have been submitted to the provider.</summary>
    public bool OnboardingSubmitted { get; private set; }

    public bool AccountsProvisioned { get; private set; }

    /// <summary>Money set aside from orders but not yet invested (euros).</summary>
    public decimal PendingAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    public void SetProviderInvestor(Guid upvestUserId)
    {
        UpvestUserId = upvestUserId;
        Touch();
    }

    public void MarkOnboardingSubmitted()
    {
        OnboardingSubmitted = true;
        Touch();
    }

    public void MarkActive()
    {
        if (Status == EnrolmentStatus.Rejected) return;
        Status = EnrolmentStatus.Active;
        Touch();
    }

    public void MarkRejected()
    {
        Status = EnrolmentStatus.Rejected;
        Touch();
    }

    public void SetAccounts(Guid accountGroupId, Guid accountId)
    {
        AccountGroupId = accountGroupId;
        AccountId = accountId;
        AccountsProvisioned = true;
        Touch();
    }

    /// <summary>Set aside the rounding from a paid order.</summary>
    public void AddSetAside(decimal amount)
    {
        if (amount <= 0m) return;
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
        Touch();
    }

    /// <summary>
    /// If the balance has reached the threshold, take the whole balance for a single investment and
    /// reset it to zero. Returns false (and takes nothing) when the balance is below the threshold.
    /// </summary>
    public bool TryTakeForInvestment(decimal threshold, out decimal amount)
    {
        amount = 0m;
        if (PendingAmount < threshold) return false;
        amount = PendingAmount;
        PendingAmount = 0m;
        Touch();
        return true;
    }

    /// <summary>Return set-aside money to the balance when an investment could not be made.</summary>
    public void ReturnToBalance(decimal amount)
    {
        if (amount <= 0m) return;
        PendingAmount = decimal.Round(PendingAmount + amount, 2, MidpointRounding.AwayFromZero);
        Touch();
    }
}
