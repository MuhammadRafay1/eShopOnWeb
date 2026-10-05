using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

public enum EnrollmentStatus
{
    /// <summary>Claimed locally; the billing system may or may not hold the subscription yet.</summary>
    Pending = 0,
    /// <summary>The billing system confirmed the subscription.</summary>
    Active = 1
}

/// <summary>
/// A buyer's enrollment in one plan. Keyed by (<see cref="BuyerId"/>, <see cref="PlanHandle"/>) and inserted
/// BEFORE the subscription is created at the billing system, so a double-click is refused by the primary key
/// rather than reaching the provider twice.
/// </summary>
public class SubscriptionEnrollment : IAggregateRoot
{
    public string BuyerId { get; private set; }

    public string PlanHandle { get; private set; }

    /// <summary>Reference sent with the subscription; used to find it again when the outcome of the write is unknown.</summary>
    public string SubscriptionReference { get; private set; }

    public EnrollmentStatus Status { get; private set; }

    public int? ProviderSubscriptionId { get; private set; }

    public DateTimeOffset ClaimedAt { get; private set; }

    public DateTimeOffset? ActivatedAt { get; private set; }

    #pragma warning disable CS8618 // Required by Entity Framework
    private SubscriptionEnrollment() { }

    public SubscriptionEnrollment(string buyerId, string planHandle, string subscriptionReference, DateTimeOffset claimedAt)
    {
        Guard.Against.NullOrWhiteSpace(buyerId, nameof(buyerId));
        Guard.Against.NullOrWhiteSpace(planHandle, nameof(planHandle));
        Guard.Against.NullOrWhiteSpace(subscriptionReference, nameof(subscriptionReference));
        BuyerId = buyerId;
        PlanHandle = planHandle;
        SubscriptionReference = subscriptionReference;
        ClaimedAt = claimedAt;
        Status = EnrollmentStatus.Pending;
    }

    public bool IsStale(DateTimeOffset now, TimeSpan claimTimeToLive) =>
        Status == EnrollmentStatus.Pending && now - ClaimedAt > claimTimeToLive;

    public void MarkActive(int providerSubscriptionId, DateTimeOffset now)
    {
        Guard.Against.NegativeOrZero(providerSubscriptionId, nameof(providerSubscriptionId));
        ProviderSubscriptionId = providerSubscriptionId;
        Status = EnrollmentStatus.Active;
        ActivatedAt = now;
    }
}
