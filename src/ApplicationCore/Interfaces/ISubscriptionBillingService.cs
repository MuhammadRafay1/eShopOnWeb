using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// A recurring subscription plan available for purchase in the billing system.
/// </summary>
public class SubscriptionPlan
{
    public string Handle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PriceInCents { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public int Interval { get; set; }
    public string IntervalUnit { get; set; } = string.Empty;
}

/// <summary>
/// The state of a user's subscription as recorded by the billing system.
/// </summary>
public class SubscriptionDetail
{
    public int Id { get; set; }
    public string PlanHandle { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int PriceInCents { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public DateTimeOffset? NextBillingAt { get; set; }
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public int CustomerId { get; set; }
    public string CustomerReference { get; set; } = string.Empty;
}

/// <summary>
/// Identifies the eShop user subscribing to (or holding) a subscription.
/// </summary>
public class Subscriber
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

/// <summary>
/// Result of a subscribe attempt. When the user already holds an open
/// subscription for the requested plan, the existing subscription is returned
/// and <see cref="AlreadySubscribed"/> is true (no duplicate is created).
/// </summary>
public class SubscribeResult
{
    public SubscribeResult(SubscriptionDetail subscription, bool alreadySubscribed)
    {
        Subscription = subscription;
        AlreadySubscribed = alreadySubscribed;
    }

    public SubscriptionDetail Subscription { get; }
    public bool AlreadySubscribed { get; }
}

/// <summary>
/// Recurring-subscription billing capability backed by Maxio Advanced Billing.
/// </summary>
public interface ISubscriptionBillingService
{
    /// <summary>
    /// Lists the subscription plans available for purchase (from the configured product family).
    /// </summary>
    Task<IReadOnlyList<SubscriptionPlan>> GetPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes the given user to the plan identified by its handle.
    /// Ensures a billing customer exists and is idempotent with respect to
    /// repeated calls for the same user and plan.
    /// </summary>
    Task<SubscribeResult> SubscribeAsync(Subscriber subscriber, string productHandle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the subscriptions currently or previously held by the given user.
    /// </summary>
    Task<IReadOnlyList<SubscriptionDetail>> GetSubscriptionsAsync(string userId, CancellationToken cancellationToken = default);
}