using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

/// <summary>
/// A shopper's enrolment into "invest your change". Owns the link to the Upvest investor (user, account
/// group and account created on their behalf), the onboarding progress, and the running change ledger — the
/// amount set aside but not yet invested, and the total invested so far. One per shopper.
/// </summary>
public class Enrolment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Enrolment() { }
#pragma warning restore CS8618

    public Enrolment(string buyerId, string taxCountry, string taxId)
    {
        BuyerId = Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        TaxCountry = Guard.Against.NullOrEmpty(taxCountry, nameof(taxCountry));
        TaxId = Guard.Against.NullOrEmpty(taxId, nameof(taxId));
        Status = EnrolmentStatus.Pending;
    }

    /// <summary>The shop's identity for the shopper (taken from the authenticated token).</summary>
    public string BuyerId { get; private set; }

    public EnrolmentStatus Status { get; private set; }

    // Onboarding data the worker needs to complete sign-up with Upvest. Never logged.
    public string TaxCountry { get; private set; }
    public string TaxId { get; private set; }

    public string? UpvestUserId { get; private set; }
    public string? UpvestAccountGroupId { get; private set; }
    public string? UpvestAccountId { get; private set; }

    public bool KycSubmitted { get; private set; }
    public bool TaxSubmitted { get; private set; }

    // Stable idempotency keys so a retried onboarding write is not duplicated at Upvest.
    public Guid CreateUserIdempotencyKey { get; private set; } = Guid.NewGuid();
    public Guid TaxIdempotencyKey { get; private set; } = Guid.NewGuid();
    public Guid AccountGroupIdempotencyKey { get; private set; } = Guid.NewGuid();
    public Guid AccountIdempotencyKey { get; private set; } = Guid.NewGuid();

    /// <summary>Change set aside for this shopper but not yet invested, in euro cents.</summary>
    public long PendingCents { get; private set; }

    /// <summary>Total successfully invested for this shopper, in euro cents.</summary>
    public long InvestedCents { get; private set; }

    /// <summary>Set when Upvest rejected the shopper. Never contains personal data.</summary>
    public string? RejectionReason { get; private set; }

    public void RecordUser(string upvestUserId) =>
        UpvestUserId = Guard.Against.NullOrEmpty(upvestUserId, nameof(upvestUserId));

    public void MarkKycSubmitted() => KycSubmitted = true;
    public void MarkTaxSubmitted() => TaxSubmitted = true;

    public void RecordAccountGroup(string accountGroupId) =>
        UpvestAccountGroupId = Guard.Against.NullOrEmpty(accountGroupId, nameof(accountGroupId));

    public void RecordAccount(string accountId) =>
        UpvestAccountId = Guard.Against.NullOrEmpty(accountId, nameof(accountId));

    /// <summary>Marks the shopper as an accepted investor (user and holding account both active).</summary>
    public void Activate()
    {
        if (Status != EnrolmentStatus.Rejected)
            Status = EnrolmentStatus.Active;
    }

    public void Reject(string reason)
    {
        Status = EnrolmentStatus.Rejected;
        RejectionReason = reason;
    }

    /// <summary>True once Upvest can hold what is bought for the shopper.</summary>
    public bool CanInvest => Status == EnrolmentStatus.Active && UpvestAccountId is not null;

    public void AddSetAside(long cents)
    {
        if (cents <= 0) return;
        PendingCents += cents;
    }

    /// <summary>Moves the given amount out of the pending ledger because an investment is being placed for it.</summary>
    public void BeginInvestment(long cents)
    {
        Guard.Against.OutOfRange(cents, nameof(cents), 1, PendingCents);
        PendingCents -= cents;
    }

    /// <summary>The placed investment filled: it is now part of the invested total.</summary>
    public void SettleInvestment(long cents) => InvestedCents += cents;

    /// <summary>The placed investment did not go through: the money returns to the pending ledger.</summary>
    public void FailInvestment(long cents) => PendingCents += cents;
}
