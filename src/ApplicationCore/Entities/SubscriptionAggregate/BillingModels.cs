using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>A recurring plan offered by the billing system.</summary>
public record SubscriptionPlan(
    int? ProviderId,
    string Handle,
    string Name,
    string? Description,
    long? PriceInCents,
    int? Interval,
    string? IntervalUnit);

/// <summary>
/// The plans offered for the configured product family. <see cref="IsTruncated"/> is true when the listing hit
/// its page cap before the provider signalled the end, i.e. more plans may exist than are listed.
/// </summary>
public record PlanCatalog(string ProductFamilyHandle, IReadOnlyList<SubscriptionPlan> Plans, bool IsTruncated)
{
    public SubscriptionPlan? Find(string planHandle) =>
        Plans.FirstOrDefault(p => string.Equals(p.Handle, planHandle, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A subscription as held by the billing system (the system of record).</summary>
public record BillingSubscription(
    int Id,
    string? Reference,
    string? PlanHandle,
    string? PlanName,
    string? State,
    bool IsTerminal,
    long? PriceInCents,
    string? Currency,
    DateTimeOffset? NextBillingAt,
    DateTimeOffset? NextAssessmentAt,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ActivatedAt);

/// <summary>The eShopOnWeb user being enrolled.</summary>
public record Subscriber(string BuyerId, string Email, string FirstName, string LastName);

/// <summary>What the billing system needs to create a customer.</summary>
public record NewBillingCustomer(string Reference, string Email, string FirstName, string LastName);

public enum SubscribeOutcome
{
    /// <summary>A new subscription was created by this request.</summary>
    Created,
    /// <summary>The buyer already holds a live subscription to this plan; nothing new was created.</summary>
    AlreadySubscribed,
    /// <summary>Another request for the same buyer and plan is still in flight.</summary>
    InProgress,
    /// <summary>The plan handle is not one the configured product family offers.</summary>
    UnknownPlan
}

public record SubscribeResult(SubscribeOutcome Outcome, BillingSubscription? Subscription, SubscriptionPlan? Plan)
{
    public static SubscribeResult Created(BillingSubscription s, SubscriptionPlan p) => new(SubscribeOutcome.Created, s, p);
    public static SubscribeResult Existing(BillingSubscription s, SubscriptionPlan p) => new(SubscribeOutcome.AlreadySubscribed, s, p);
    public static SubscribeResult InProgress(SubscriptionPlan p) => new(SubscribeOutcome.InProgress, null, p);
    public static SubscribeResult UnknownPlan() => new(SubscribeOutcome.UnknownPlan, null, null);
}
